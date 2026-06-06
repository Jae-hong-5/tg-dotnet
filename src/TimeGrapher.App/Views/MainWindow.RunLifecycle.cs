using System;
using System.Globalization;
using System.Threading.Tasks;

using Avalonia.Threading;

using TimeGrapher.App.Audio;
using TimeGrapher.App.Services;
using TimeGrapher.Core.Analysis;
using TimeGrapher.Core.AudioIo;
using TimeGrapher.Core.Shared;
using TimeGrapher.Core.Sim;

namespace TimeGrapher.App.Views;

public partial class MainWindow
{
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // ~MainWindow: StopAnalysisThread(); plus stop any running input worker.
        mIsClosing = true;
        mViewModel.PropertyChanged -= mSelectionCoordinator.OnViewModelPropertyChanged;
        InvalidateRunSession();
        StopInputWorker("Input");
        StopAnalysisThread();
        AudioCloseCheck();
    }

    private ulong BeginRunSession()
    {
        unchecked
        {
            mRunSessionToken++;
            if (mRunSessionToken == 0)
            {
                mRunSessionToken = 1;
            }

            return mRunSessionToken;
        }
    }

    private void InvalidateRunSession()
    {
        _ = BeginRunSession();
    }

    private void StartAudioThread()
    {
        int deviceNumber = CurrentInputDeviceNumber();
        if (deviceNumber < 0)
        {
            throw new InvalidOperationException("No live audio device is selected.");
        }

        MasterAudioBuffer buffer = PrepareInputRun(out ulong runSessionToken);

        ILiveAudioWorker audioWorker = LiveAudioBackend.CreateWorker(buffer);
        AttachInputWorker(audioWorker, runSessionToken);
        audioWorker.Start(deviceNumber, mCurrentSamplesPerSecond, (float)(mViewModel.Gain / 1000.0));
    }

    private StopOutcome StopAudioThread()
    {
        // LocalStopAudio -> StopAudioRecording.
        return StopInputWorker("Audio");
    }

    private StopOutcome StopInputWorker(string workerName)
    {
        IAudioInputWorker? worker = mInputWorker;
        if (worker != null)
        {
            if (mInputDataReadyHandler != null)
            {
                worker.DataReady -= mInputDataReadyHandler;
                mInputDataReadyHandler = null;
            }

            if (!worker.TryStop(TimeSpan.FromMilliseconds(WORKER_STOP_TIMEOUT_MS)))
            {
                mViewModel.StatusText = workerName + " worker did not stop within timeout";
                return StopOutcome.Stopping;
            }

            mInputCompletionDetach?.Invoke();
            mInputCompletionDetach = null;
            worker.Dispose();
            mInputWorker = null;
        }

        return StopOutcome.Stopped;
    }

    private void StartPlaybackThread(string fileName)
    {
        MasterAudioBuffer buffer = PrepareInputRun(out ulong runSessionToken);

        var playbackWorker = new PlaybackWorker(buffer, mCurrentSamplesPerSecond);
        Action<PlaybackCompletionReason> doneHandler = reason => OnPlaybackDoneReadingFile(runSessionToken, reason);
        playbackWorker.DoneReadingFile += doneHandler;
        AttachInputWorker(playbackWorker, runSessionToken, () => playbackWorker.DoneReadingFile -= doneHandler);
        if (!playbackWorker.Start(fileName))
        {
            throw new InvalidOperationException("Playback worker is already running.");
        }
    }

    private void StartSimThread(WatchSynthStreamConfig cfg)
    {
        MasterAudioBuffer buffer = PrepareInputRun(out ulong runSessionToken);

        var simWorker = new SimWorker(buffer, mCurrentSamplesPerSecond);
        Action<SimCompletionReason> doneHandler = reason => OnSimDone(runSessionToken, reason);
        simWorker.SimDone += doneHandler;
        AttachInputWorker(simWorker, runSessionToken, () => simWorker.SimDone -= doneHandler);
        if (!simWorker.Start(cfg))
        {
            throw new InvalidOperationException("Sim worker is already running.");
        }
    }

    private StopOutcome StopPlaybackThread()
    {
        // requestInterruption(): cancel; the worker reports completion via DoneReadingFile,
        // but on_StopPushButton_clicked also calls StopAnalysisThread()/AudioCloseCheck() directly.
        return StopInputWorker("Playback");
    }

    private StopOutcome StopSimThread()
    {
        return StopInputWorker("Sim");
    }

    private MasterAudioBuffer PrepareInputRun(out ulong runSessionToken)
    {
        runSessionToken = BeginRunSession();
        StopAnalysisThread();
        Reset();

        // Recreate the master buffer at the current sample rate before analysis starts.
        var buffer = new MasterAudioBuffer(mCurrentSamplesPerSecond);
        mRawAudio = buffer;
        StartAnalysisThread();

        return buffer;
    }

    private void StartAnalysisThread()
    {
        mAnalysisSessionId++;

        AnalysisWorker.Config analysisConfig = BuildRunSettings().ToWorkerConfig(mAnalysisSessionId, mWavWriter);

        mAnalysisWorker = new AnalysisWorker(mRawAudio!, analysisConfig);
        mAnalysisWorker.AnalysisFrameReady += OnAnalysisFrameReady;
        mAnalysisWorker.Start();
    }

    private StopOutcome StopAnalysisThread(bool completeInput = false)
    {
        if (mAnalysisWorker != null)
        {
            bool stopped = completeInput
                ? mAnalysisWorker.CompleteInput(TimeSpan.FromMilliseconds(WORKER_STOP_TIMEOUT_MS))
                : mAnalysisWorker.TryStop(TimeSpan.FromMilliseconds(WORKER_STOP_TIMEOUT_MS));
            if (stopped)
            {
                mAnalysisWorker.AnalysisFrameReady -= OnAnalysisFrameReady;
                mAnalysisWorker.Dispose();
                mAnalysisWorker = null;
                if (completeInput)
                {
                    mFrameRenderScheduler.ResetTiming();
                }
                else
                {
                    mAnalysisSessionId++;
                    ClearPendingAnalysisFrames();
                }
            }
            else
            {
                mViewModel.StatusText = "Analysis worker did not stop within timeout";
                return StopOutcome.Stopping;
            }
        }
        else
        {
            ClearPendingAnalysisFrames();
        }

        return StopOutcome.Stopped;
    }

    // Input worker DataReady (any thread) -> analysis worker. Safe from any thread.
    private Action CreateDataReadyHandler(ulong runSessionToken)
    {
        return () =>
        {
            if (runSessionToken == mRunSessionToken)
            {
                mAnalysisWorker?.NotifyDataReady();
            }
        };
    }

    private void AttachInputWorker(
        IAudioInputWorker worker,
        ulong runSessionToken,
        Action? detachCompletion = null)
    {
        mInputWorker = worker;
        mInputCompletionDetach = detachCompletion;
        mInputDataReadyHandler = CreateDataReadyHandler(runSessionToken);
        worker.DataReady += mInputDataReadyHandler;
    }

    private void OnPlaybackDoneReadingFile(ulong runSessionToken, PlaybackCompletionReason reason)
    {
        // PlaybackDoneReadingFile fires on the playback thread; marshal to UI thread.
        Dispatcher.UIThread.Post(() => HandlePlaybackDoneReadingFile(runSessionToken, reason));
    }

    private void OnSimDone(ulong runSessionToken, SimCompletionReason reason)
    {
        Dispatcher.UIThread.Post(() => HandleSimDone(runSessionToken, reason));
    }

    private void HandlePlaybackDoneReadingFile(ulong runSessionToken, PlaybackCompletionReason reason)
    {
        CompletePlaybackOrSimulationRun(
            runSessionToken,
            shouldRestoreAudioState: CurrentModeText() == ModeStrings[PLAYBACK],
            stopInputWorker: () => StopInputWorker("Playback"),
            failureStatus: "Playback failed",
            failed: reason == PlaybackCompletionReason.Failed);
    }

    private void HandleSimDone(ulong runSessionToken, SimCompletionReason reason)
    {
        CompletePlaybackOrSimulationRun(
            runSessionToken,
            shouldRestoreAudioState: CurrentModeText() == ModeStrings[SIM],
            stopInputWorker: () => StopInputWorker("Sim"),
            failureStatus: "Simulation failed",
            failed: reason == SimCompletionReason.Failed);
    }

    private void CompletePlaybackOrSimulationRun(
        ulong runSessionToken,
        bool shouldRestoreAudioState,
        Func<StopOutcome> stopInputWorker,
        string failureStatus,
        bool failed)
    {
        if (runSessionToken != mRunSessionToken)
        {
            return;
        }

        InvalidateRunSession();
        SetGuiStoppingMode();
        if (shouldRestoreAudioState)
        {
            RestorePlaybackOrSimulationAudioState();
        }

        StopOutcome outcome = stopInputWorker();
        outcome = CombineStopOutcome(outcome, StopAnalysisThread(completeInput: true));
        bool audioClosed = outcome == StopOutcome.Stopped && AudioCloseCheck();
        if (outcome != StopOutcome.Stopped || !audioClosed)
        {
            SetGuiStoppingMode();
            return;
        }

        SetGuiStopMode();
        mViewModel.StatusText = failed ? failureStatus : "Stopped";
    }

    private async Task<bool> RecordSessionCheck()
    {
        RecordingSessionStartResult result = await mRecordingSessionService.TryStartAsync(mCurrentSamplesPerSecond);
        if (result.Writer != null)
        {
            mWavWriter = result.Writer;
        }

        return result.ShouldContinue;
    }

    private bool AudioCloseCheck()
    {
        if (mWavWriter != null)
        {
            ulong droppedBlocks = mWavWriter.DroppedBlocks;
            bool closed = mWavWriter.Close();
            if (!closed)
            {
                mViewModel.StatusText = "Failed to close WAV recording cleanly";
                return false;
            }

            mWavWriter.Dispose();
            mWavWriter = null;
            if (droppedBlocks != 0)
            {
                mViewModel.StatusText = "WAV recording dropped " +
                                     droppedBlocks.ToString(CultureInfo.InvariantCulture) +
                                     " block(s)";
            }
        }

        return true;
    }

    private void SetGuiRunMode()
    {
        mViewModel.SetRunning();
    }

    private void SetGuiStartingMode()
    {
        mViewModel.SetStarting();
    }

    private void SetGuiStoppingMode()
    {
        mViewModel.SetStopping();
    }

    private void SetGuiStopMode()
    {
        mViewModel.SetModeAllowsSampleRate(CurrentModeText() != ModeStrings[PLAYBACK]);
        mViewModel.SetStopped();
    }

    private async Task<bool> LiveStart()
    {
        if (!await RecordSessionCheck())
        {
            return false;
        }

        try
        {
            StartAudioThread();
        }
        catch (Exception ex)
        {
            InvalidateRunSession();
            StopInputWorker("Audio");
            StopAnalysisThread();
            AudioCloseCheck();
            mViewModel.StatusText = "Failed to start live audio";
            await mDialogs.ShowErrorAsync("Error", "Failed to start live audio: " + ex.Message);
            return false;
        }

        SetGuiRunMode();
        mViewModel.StatusText = "Running";
        return true;
    }

    private async Task<bool> PlaybackStart()
    {
        PlaybackFileSelectionResult selection = await mPlaybackFileService.SelectPlaybackFileAsync(mCurrentDir);
        if (!selection.Selected || selection.FilePath == null)
        {
            if (!string.IsNullOrEmpty(selection.StatusMessage))
            {
                mViewModel.StatusText = selection.StatusMessage;
            }

            return false;
        }

        mCurrentDir = selection.CurrentDirectory;
        GetAudioRate(out mRateBeforePlaybackOrSim);
        GetAudioDevice(out mDeviceNameBeforePlaybackOrSim);
        if (!SetAudioDevice(PLAYBACK_OR_SIM_PCM))
        {
            Console.Error.WriteLine("SetAudioDevice Failed");
        }

        if (!SetAudioRate(selection.SampleRate))
        {
            Console.Error.WriteLine("SetAudioRate Failed");
            return false;
        }

        if (!await RecordSessionCheck())
        {
            RestorePlaybackOrSimulationAudioState();
            return false;
        }

        StartPlaybackThread(selection.FilePath);
        SetGuiRunMode();
        mViewModel.StatusText = "Running";
        return true;
    }

    private async Task<bool> SimStart()
    {
        // RealisticCheckBox -> realistic config; otherwise clean config
        // (MainWindow.cpp: watch_synth_stream_realistic_config / watch_synth_stream_clean_config).
        WatchSynthStreamConfig cfg = mViewModel.Realistic
            ? WatchSynthStreamConfig.Realistic()
            : WatchSynthStreamConfig.Clean();

        SimulationSelection selection = mRunSelectionResolver.GetSimulationSelection(mAvailableRates, mNumberOfRates);
        cfg.Bph = selection.Bph;
        cfg.SampleRateHz = (uint)selection.SampleRate;
        cfg.BeatErrorMs = -(double)mViewModel.SimBeatError;
        cfg.PcmPeakAmplitude = 0.40; // normalized float PCM digital output level
        cfg.WatchAmplitudeDegrees = (double)mViewModel.SimAmplitude;
        cfg.LiftAngleDegrees = (double)mViewModel.LiftAngle;
        cfg.RateErrorSPerDay = (double)mViewModel.SimErrorRate;

        if (!await RecordSessionCheck())
        {
            return false;
        }

        GetAudioRate(out mRateBeforePlaybackOrSim);
        GetAudioDevice(out mDeviceNameBeforePlaybackOrSim);
        if (!SetAudioDevice(PLAYBACK_OR_SIM_PCM))
        {
            Console.Error.WriteLine("SetAudioDevice Failed");
        }

        if (!SetAudioRate(mRateBeforePlaybackOrSim))
        {
            Console.Error.WriteLine("SetAudioRate Failed");
        }

        StartSimThread(cfg);
        SetGuiRunMode();
        mViewModel.StatusText = "Running";
        return true;
    }

    private async Task StartRunAsync()
    {
        await mRunCommandService.StartAsync();
    }

    private void TogglePauseRun()
    {
        mRunCommandService.TogglePause();
    }

    private void SetWorkersPaused(bool paused)
    {
        mInputWorker?.SetPaused(paused);
    }

    private void StopRun()
    {
        mRunCommandService.Stop();
    }
}
