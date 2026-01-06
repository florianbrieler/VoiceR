using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Whisper.net;
using Whisper.net.Ggml;

namespace VoiceR.Voice
{
    public class WhisperConverter : IConverter, IDisposable
    {
        // constants
        private static readonly IReadOnlyList<ConverterModel> _availableModels = new List<ConverterModel>
        {
            new ConverterModel(GgmlType.Tiny, "Tiny (~75MB)"),
            new ConverterModel(GgmlType.Base, "Base (~142MB)"),
            new ConverterModel(GgmlType.Small, "Small (~466MB)"),
            new ConverterModel(GgmlType.Medium, "Medium (~1.5GB)"),
            new ConverterModel(GgmlType.LargeV2, "Large V2 (~2.9GB)"),
        };
        private static readonly string Language = "de";
        private static readonly ConverterModel DefaultModelType = _availableModels[1];
        private static readonly string ModelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoiceR", "Models");

        // Whisper
        private WhisperProcessor? _whisperProcessor;
        private WhisperFactory? _whisperFactory;

        private string? _currentModelPath;
        private ConverterModel _currentModel = DefaultModelType;

        // dependencies
        private ILogger _logger;

        public WhisperConverter(ILogger logger)
        {
            // dependencies
            _logger = logger;
        }

        public bool IsInitialized => _whisperProcessor != null && _whisperFactory != null;

        public List<ConverterModel> AvailableModels => _availableModels.ToList();

        public ConverterModel SelectedModel => _currentModel;

        public async Task Initialize(ConverterModel? model = null)
        {
            if (model == null)
            {
                model = DefaultModelType;
            }
            if (model.ModelType is not GgmlType ggmlType)
            {
                throw new Exception("Invalid model type");
            }

            // Dispose existing resources if switching models
            if (_currentModel != model && _whisperProcessor != null)
            {
                Dispose();
            }

            _currentModel = model;
            string modelFileName = GetModelFileName(ggmlType);
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
                    using var modelStream = await downloader.GetGgmlModelAsync(ggmlType);
                    using var fileStream = File.Create(_currentModelPath);
                    await modelStream.CopyToAsync(fileStream);
                }

                // Initialize Whisper
                _whisperFactory = WhisperFactory.FromPath(_currentModelPath);
                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage(Language)
                    .Build();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Whisper model");
                throw new Exception($"Failed to initialize Whisper model: {ex.Message}", ex);
            }
        }

        public async Task<string> Transcribe(AudioData audioData)
        {
            if (_whisperProcessor == null)
            {
                _logger.LogError("Whisper processor not initialized");
                return string.Empty;
            }

            try
            {
                List<string> segments = new List<string>();
                await foreach (SegmentData segment in _whisperProcessor.ProcessAsync(audioData.data, CancellationToken.None))
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
                _logger.LogError(ex, "Failed to transcribe");
                return string.Empty;
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

        public void Dispose()
        {
            _whisperProcessor?.Dispose();
            _whisperProcessor = null;

            _whisperFactory?.Dispose();
            _whisperFactory = null;
        }
    }
}

