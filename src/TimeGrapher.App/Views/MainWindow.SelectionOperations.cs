using System.Collections.Generic;

using TimeGrapher.App.Audio;
using TimeGrapher.App.Services;

namespace TimeGrapher.App.Views;

public partial class MainWindow
{
    private sealed class MainWindowSelectionOperations : IMainWindowSelectionOperations
    {
        private readonly MainWindow _owner;

        public MainWindowSelectionOperations(MainWindow owner)
        {
            _owner = owner;
        }

        public IReadOnlyList<int> InputDeviceNumbers => _owner.mInputDeviceNumbers;

        public int AvailableSampleRateCount => _owner.mNumberOfRates;

        public int GetAvailableSampleRate(int index)
        {
            return _owner.mAvailableRates[index];
        }

        public void PopulateSampleRates(int deviceNumber)
        {
            _owner.PopulateSampleRates(deviceNumber);
        }

        public void SetCurrentSampleRate(int sampleRate)
        {
            _owner.mCurrentSamplesPerSecond = sampleRate;
        }

        public void SetAudioInputVolume(float normalizedVolume)
        {
            if (_owner.mInputWorker is ILiveAudioWorker liveWorker)
            {
                liveWorker.SetVolume(normalizedVolume);
            }
        }
    }
}
