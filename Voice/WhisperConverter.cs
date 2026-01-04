using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceR.Voice
{
    public class WhisperConverter : IDisposable
    {
        private WhisperProcessor? _whisperProcessor;
        private WhisperFactory? _whisperFactory;
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioBuffer;
        private BinaryWriter? _audioWriter;
        private bool _isRecording = false;
        private readonly object _audioLock = new object();

        // Whisper requires 16kHz mono audio
        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;

        private static readonly string ModelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceR", "Models");

        private string? _currentModelPath;
        private GgmlType _currentModelType;
        private string? _lastRecordingPath;
        private WaveOutEvent? _waveOut;

        public event EventHandler<bool>? RecordingStateChanged;

        public bool IsRecording => _isRecording;

        public bool IsInitialized => _whisperProcessor != null && _whisperFactory != null;

        public async Task InitializeAsync(GgmlType modelType)
        {
            // Dispose existing resources if switching models
            if (_currentModelType != modelType && _whisperProcessor != null)
            {
                _whisperProcessor?.Dispose();
                _whisperFactory?.Dispose();
                _whisperProcessor = null;
                _whisperFactory = null;
            }

            _currentModelType = modelType;
            string modelFileName = GetModelFileName(modelType);
            _currentModelPath = Path.Combine(ModelDirectory, modelFileName);

            try
            {
                // Ensure model directory exists
                Directory.CreateDirectory(ModelDirectory);

                // Download model if not present
                if (!File.Exists(_currentModelPath))
                {
                    using var httpClient = new HttpClient();
                    var downloader = new WhisperGgmlDownloader(httpClient);
                    using var modelStream = await downloader.GetGgmlModelAsync(modelType);
                    using var fileStream = File.Create(_currentModelPath);
                    await modelStream.CopyToAsync(fileStream);
                }

                // Initialize Whisper
                _whisperFactory = WhisperFactory.FromPath(_currentModelPath);
                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage("de") // Hardcoded to German as per plan
                    .Build();

                // Initialize audio capture (if not already initialized)
                if (_waveIn == null)
                {
                    _waveIn = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                        BufferMilliseconds = 100
                    };
                    _waveIn.DataAvailable += WaveIn_DataAvailable;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to initialize Whisper model: {ex.Message}", ex);
            }
        }

        private string GetModelFileName(GgmlType modelType)
        {
            return modelType switch
            {
                GgmlType.Tiny => "ggml-tiny.bin",
                GgmlType.Base => "ggml-base.bin",
                GgmlType.Small => "ggml-small.bin",
                GgmlType.Medium => "ggml-medium.bin",
                GgmlType.LargeV2 => "ggml-large-v2.bin",
                GgmlType.LargeV3 => "ggml-large-v3.bin",
                _ => "ggml-base.bin"
            };
        }

        public void StartRecording()
        {
            if (_waveIn == null || _isRecording)
                return;

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
                throw new Exception($"Failed to start recording: {ex.Message}", ex);
            }
        }

        public async Task<string> StopRecordingAsync()
        {
            if (_waveIn == null || !_isRecording)
                return string.Empty;

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
                    return string.Empty;
                }

                // Save to temporary file for replay
                _lastRecordingPath = Path.Combine(Path.GetTempPath(), "VoiceR_Recording.wav");
                SaveAudioToWavFile(audioData, _lastRecordingPath);

                // Transcribe the audio
                if (_whisperProcessor == null)
                {
                    throw new InvalidOperationException("Whisper processor not initialized");
                }

                // Convert bytes to float samples (Whisper expects float32)
                var samples = ConvertBytesToFloatSamples(audioData);

                // Process with Whisper
                var segments = new System.Collections.Generic.List<string>();
                await foreach (var segment in _whisperProcessor.ProcessAsync(samples, CancellationToken.None))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segments.Add(segment.Text.Trim());
                    }
                }

                return string.Join(" ", segments);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to stop recording and transcribe: {ex.Message}", ex);
            }
        }

        public async Task ReplayLastRecordingAsync()
        {
            if (string.IsNullOrEmpty(_lastRecordingPath) || !File.Exists(_lastRecordingPath))
            {
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
            _waveIn = null;

            _audioWriter?.Dispose();
            _audioWriter = null;

            _audioBuffer?.Dispose();
            _audioBuffer = null;

            _whisperProcessor?.Dispose();
            _whisperProcessor = null;

            _whisperFactory?.Dispose();
            _whisperFactory = null;
        }
    }
}

