using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace VoiceR.Voice
{
    public class NAudioRecorder : IRecorder, IDisposable
    {
        private WaveInEvent _waveIn;

        private MemoryStream? _audioBuffer;
        private BinaryWriter? _audioWriter;
        private readonly object _audioLock = new object();

        private WaveOutEvent? _waveOut;

        private string? _lastRecordingPath;

        private bool _isRecording = false;
        public bool IsRecording => _isRecording;
        public event EventHandler<bool>? RecordingStateChanged;

        // Whisper requires 16kHz mono audio
        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;

        // dependencies
        private ILogger _logger;

        public NAudioRecorder(ILogger logger)
        {
            // dependencies
            _logger = logger;

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };
            _waveIn.DataAvailable += WaveIn_DataAvailable;
        }

        public void StartRecording()
        {
            if (_isRecording)
            {
                return;
            }

            try
            {
                _audioBuffer = new MemoryStream();
                _audioWriter = new BinaryWriter(_audioBuffer);

                _waveIn.StartRecording();
                _isRecording = true;

                RecordingStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start recording");
            }
        }

        public AudioData StopRecording()
        {
            if (!_isRecording)
            {
                return new AudioData();
            }

            try
            {
                _waveIn.StopRecording();
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);

                // Get the recorded audio data
                byte[] audioData;
                lock (_audioLock)
                {
                    audioData = _audioBuffer?.ToArray() ?? Array.Empty<byte>();
                }

                // Clean up recording resources
                _audioWriter?.Dispose();
                _audioWriter = null;
                _audioBuffer?.Dispose();
                _audioBuffer = null;

                if (audioData.Length < SampleRate * 2) // At least 1 second of audio
                {
                    return new AudioData();
                }

                // Save to temporary file for replay
                _lastRecordingPath = Path.Combine(Path.GetTempPath(), "VoiceR_Recording.wav");
                SaveAudioToWavFile(audioData, _lastRecordingPath);

                float[] samples = ConvertBytesToFloatSamples(audioData);

                return new AudioData { data = samples };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop recording");
                return new AudioData();
            }
        }

        public async Task ReplayLastRecording()
        {
            if (string.IsNullOrEmpty(_lastRecordingPath) || !File.Exists(_lastRecordingPath))
            {
                _logger.LogError("No recording available to replay");
                throw new InvalidOperationException("No recording available to replay");
            }

            try
            {
                // Stop any currently playing audio
                _waveOut?.Stop();
                _waveOut?.Dispose();

                // Play the audio file
                _waveOut = new WaveOutEvent();
                using var audioFile = new AudioFileReader(_lastRecordingPath);
                _waveOut.Init(audioFile);
                _waveOut.Play();

                // Wait for playback to complete
                await Task.Run(() =>
                {
                    while (_waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(100);
                    }
                });

                _waveOut?.Dispose();
                _waveOut = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay recording");
                throw new Exception($"Failed to replay recording: {ex.Message}", ex);
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isRecording || _audioWriter == null) return;

            lock (_audioLock)
            {
                _audioWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }

        private float[] ConvertBytesToFloatSamples(byte[] audioData)
        {
            int sampleCount = audioData.Length / 2; // 16-bit = 2 bytes per sample
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                short sample = BitConverter.ToInt16(audioData, i * 2);
                samples[i] = sample / 32768f; // Normalize to [-1, 1]
            }

            return samples;
        }

        private void SaveAudioToWavFile(byte[] audioData, string filePath)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(fileStream);

            // Write WAV header
            int dataSize = audioData.Length;
            int fileSize = 36 + dataSize;

            // RIFF header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // fmt chunk size
            writer.Write((short)1); // audio format (1 = PCM)
            writer.Write((short)Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * BitsPerSample / 8); // byte rate
            writer.Write((short)(Channels * BitsPerSample / 8)); // block align
            writer.Write((short)BitsPerSample);

            // data chunk
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            writer.Write(audioData);
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                try
                {
                    _waveIn?.StopRecording();
                }
                catch { }
            }

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _waveIn?.Dispose();
            // _waveIn = null;

            _audioWriter?.Dispose();
            _audioWriter = null;

            _audioBuffer?.Dispose();
            _audioBuffer = null;
        }
    }
}

