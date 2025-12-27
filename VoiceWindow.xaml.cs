using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceR
{
    public sealed partial class VoiceWindow : Window
    {
        private WhisperProcessor? _whisperProcessor;
        private WhisperFactory? _whisperFactory;
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioBuffer;
        private BinaryWriter? _audioWriter;
        private StringBuilder _transcriptionBuilder = new StringBuilder();
        private bool _isListening = false;
        private CancellationTokenSource? _processingCts;
        private readonly object _audioLock = new object();
        
        // Whisper requires 16kHz mono audio
        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;
        
        // Process audio every N seconds
        private const int ProcessingIntervalSeconds = 3;
        private DateTime _lastProcessTime = DateTime.MinValue;
        
        private static readonly string ModelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceR", "Models");
        
        private static readonly string ModelPath = Path.Combine(ModelDirectory, "ggml-base.bin");

        public VoiceWindow()
        {
            this.InitializeComponent();
            
            Title = "VoiceR - Voice Transcription";
            
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(700, 500));
            
            this.Activated += VoiceWindow_Activated;
            this.Closed += VoiceWindow_Closed;
        }

        private bool _hasInitialized = false;

        private async void VoiceWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (_hasInitialized) return;
            _hasInitialized = true;

            await InitializeWhisperAsync();
        }

        private async Task InitializeWhisperAsync()
        {
            try
            {
                UpdateStatus("Checking model...", "#FF9800");
                
                // Ensure model directory exists
                Directory.CreateDirectory(ModelDirectory);
                
                // Download model if not present
                if (!File.Exists(ModelPath))
                {
                    UpdateStatus("Downloading model (~142MB)...", "#FF9800");
                    ModelInfoText.Text = "Downloading ggml-small...";
                    
                    using var httpClient = new HttpClient();
                    var downloader = new WhisperGgmlDownloader(httpClient);
                    using var modelStream = await downloader.GetGgmlModelAsync(GgmlType.Small);
                    using var fileStream = File.Create(ModelPath);
                    await modelStream.CopyToAsync(fileStream);
                }
                
                ModelInfoText.Text = "ggml-small (Whisper)";
                UpdateStatus("Loading model...", "#FF9800");
                
                // Initialize Whisper
                _whisperFactory = WhisperFactory.FromPath(ModelPath);
                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage("de") // alternative: "auto"
                    .Build();
                
                // Initialize audio capture
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                    BufferMilliseconds = 100
                };
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                
                UpdateStatus("Ready", "#4CAF50");
                StartStopButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", "#F44336");
                ModelInfoText.Text = "Failed to load";
                TranscriptionText.Text = $"Failed to initialize Whisper.\n\nError: {ex.Message}\n\nPlease check:\n- Internet connection (for model download)\n- Sufficient disk space (~150MB needed)";
            }
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isListening || _audioWriter == null) return;
            
            lock (_audioLock)
            {
                _audioWriter.Write(e.Buffer, 0, e.BytesRecorded);
            }
            
            // Check if we should process the audio
            if ((DateTime.Now - _lastProcessTime).TotalSeconds >= ProcessingIntervalSeconds)
            {
                _lastProcessTime = DateTime.Now;
                _ = ProcessAudioAsync();
            }
        }

        private async Task ProcessAudioAsync()
        {
            if (_whisperProcessor == null || _audioBuffer == null) return;
            
            byte[] audioData;
            lock (_audioLock)
            {
                audioData = _audioBuffer.ToArray();
                // Reset buffer but keep some overlap for continuity
                _audioBuffer.SetLength(0);
                _audioBuffer.Position = 0;
            }
            
            if (audioData.Length < SampleRate * 2) // At least 1 second of audio
                return;
            
            try
            {
                // Convert bytes to float samples (Whisper expects float32)
                var samples = ConvertBytesToFloatSamples(audioData);
                
                // Process with Whisper
                var segments = new List<string>();
                await foreach (var segment in _whisperProcessor.ProcessAsync(samples, _processingCts?.Token ?? CancellationToken.None))
                {
                    if (!string.IsNullOrWhiteSpace(segment.Text))
                    {
                        segments.Add(segment.Text.Trim());
                    }
                }
                
                if (segments.Count > 0)
                {
                    var transcribedText = string.Join(" ", segments);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        AppendTranscription(transcribedText);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // Log error but don't crash
                    System.Diagnostics.Debug.WriteLine($"Whisper processing error: {ex.Message}");
                });
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

        private void AppendTranscription(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (_transcriptionBuilder.Length > 0)
                {
                    _transcriptionBuilder.Append(" ");
                }
                _transcriptionBuilder.Append(text);
                UpdateTranscriptionDisplay();
            }
        }

        private void UpdateTranscriptionDisplay()
        {
            string displayText = _transcriptionBuilder.ToString();

            if (string.IsNullOrEmpty(displayText))
            {
                displayText = _isListening ? "Listening... speak now." : "Transcribed speech will appear here...";
            }

            TranscriptionText.Text = displayText;
            
            // Auto-scroll to bottom
            TranscriptionScrollViewer.UpdateLayout();
            TranscriptionScrollViewer.ChangeView(null, TranscriptionScrollViewer.ScrollableHeight, null);
        }

        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isListening)
            {
                StopListening();
            }
            else
            {
                StartListening();
            }
        }

        private void StartListening()
        {
            if (_waveIn == null) return;

            try
            {
                _processingCts = new CancellationTokenSource();
                _audioBuffer = new MemoryStream();
                _audioWriter = new BinaryWriter(_audioBuffer);
                _lastProcessTime = DateTime.Now;
                
                _waveIn.StartRecording();
                _isListening = true;
                StartStopButton.Content = "Stop Listening";
                UpdateStatus("Listening", "#4CAF50");
                UpdateTranscriptionDisplay();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", "#F44336");
            }
        }

        private async void StopListening()
        {
            if (_waveIn == null) return;

            try
            {
                _waveIn.StopRecording();
                _isListening = false;
                StartStopButton.Content = "Start Listening";
                UpdateStatus("Processing final audio...", "#FF9800");
                
                // Process any remaining audio
                await ProcessAudioAsync();
                
                _processingCts?.Cancel();
                _processingCts?.Dispose();
                _processingCts = null;
                
                _audioWriter?.Dispose();
                _audioWriter = null;
                _audioBuffer?.Dispose();
                _audioBuffer = null;
                
                UpdateStatus("Stopped", "#FF9800");
                UpdateTranscriptionDisplay();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error: {ex.Message}", "#F44336");
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _transcriptionBuilder.Clear();
            UpdateTranscriptionDisplay();
        }

        private void UpdateStatus(string status, string colorHex)
        {
            StatusText.Text = status;
            
            colorHex = colorHex.TrimStart('#');
            byte r = Convert.ToByte(colorHex.Substring(0, 2), 16);
            byte g = Convert.ToByte(colorHex.Substring(2, 2), 16);
            byte b = Convert.ToByte(colorHex.Substring(4, 2), 16);
            
            StatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
        }

        private void VoiceWindow_Closed(object sender, WindowEventArgs args)
        {
            // Clean up
            _processingCts?.Cancel();
            
            if (_waveIn != null)
            {
                try
                {
                    if (_isListening)
                    {
                        _waveIn.StopRecording();
                    }
                    _waveIn.Dispose();
                }
                catch { }
                _waveIn = null;
            }
            
            _audioWriter?.Dispose();
            _audioBuffer?.Dispose();
            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
            _processingCts?.Dispose();
        }
    }
}
