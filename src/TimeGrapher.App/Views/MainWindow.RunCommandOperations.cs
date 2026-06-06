using System;
using System.Threading.Tasks;

using TimeGrapher.App.Services;

namespace TimeGrapher.App.Views;

public partial class MainWindow
{
    private sealed class RunCommandOperations : IRunCommandOperations
    {
        private readonly MainWindow _owner;

        public RunCommandOperations(MainWindow owner)
        {
            _owner = owner;
        }

        public bool IsClosing => _owner.mIsClosing;

        public bool HasActiveWorker => _owner.mInputWorker != null;

        public RunCommandMode CurrentMode
        {
            get
            {
                string mode = _owner.CurrentModeText();
                if (mode == ModeStrings[LIVE])
                {
                    return RunCommandMode.Live;
                }

                if (mode == ModeStrings[PLAYBACK])
                {
                    return RunCommandMode.Playback;
                }

                if (mode == ModeStrings[SIM])
                {
                    return RunCommandMode.Simulation;
                }

                return RunCommandMode.Unknown;
            }
        }

        public void ConfigureLiveAudio()
        {
            _owner.ConfigureSoundCard();
        }

        public Task<bool> StartLiveAsync()
        {
            return _owner.LiveStart();
        }

        public Task<bool> StartPlaybackAsync()
        {
            return _owner.PlaybackStart();
        }

        public Task<bool> StartSimulationAsync()
        {
            return _owner.SimStart();
        }

        public void SetWorkersPaused(bool paused)
        {
            _owner.SetWorkersPaused(paused);
        }

        public void CleanupFailedStart()
        {
            _owner.InvalidateRunSession();
            _owner.StopInputWorker("Input");
            _owner.StopAnalysisThread();
            _owner.AudioCloseCheck();
        }

        public Task ShowStartFailureAsync(Exception exception)
        {
            return _owner.mDialogs.ShowErrorAsync("Error", "Failed to start: " + exception.Message);
        }

        public RunCommandStopOutcome StopLive()
        {
            StopOutcome outcome = CombineStopOutcome(_owner.StopAudioThread(), _owner.StopAnalysisThread());
            return MapStopOutcome(outcome);
        }

        public RunCommandStopOutcome StopPlayback()
        {
            StopOutcome outcome = CombineStopOutcome(_owner.StopPlaybackThread(), _owner.StopAnalysisThread());
            return MapStopOutcome(outcome);
        }

        public RunCommandStopOutcome StopSimulation()
        {
            StopOutcome outcome = CombineStopOutcome(_owner.StopSimThread(), _owner.StopAnalysisThread());
            return MapStopOutcome(outcome);
        }

        public bool CloseAudio()
        {
            return _owner.AudioCloseCheck();
        }

        public void InvalidateRunSession()
        {
            _owner.InvalidateRunSession();
        }

        public void RestorePlaybackOrSimulationAudioState()
        {
            _owner.RestorePlaybackOrSimulationAudioState();
        }

        private static RunCommandStopOutcome MapStopOutcome(StopOutcome outcome)
        {
            return outcome == StopOutcome.Stopped
                ? RunCommandStopOutcome.Stopped
                : RunCommandStopOutcome.Stopping;
        }
    }
}
