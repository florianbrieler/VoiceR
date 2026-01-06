using System;
using System.Threading.Tasks;

namespace VoiceR.Voice
{
    public interface IRecorder
    {
        public event EventHandler<bool>? RecordingStateChanged;
        public bool IsRecording { get; }

        public void StartRecording();
        public AudioData StopRecording();
        public Task ReplayLastRecording();
    }
}

