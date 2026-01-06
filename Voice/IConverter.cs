using System.Collections.Generic;
using System.Threading.Tasks;

namespace VoiceR.Voice
{
    public interface IConverter
    {
        public bool IsInitialized { get; }

        public List<ConverterModel> AvailableModels { get; }
        public ConverterModel SelectedModel { get; }
        public Task Initialize(ConverterModel? model = null);
        public Task<string> Transcribe(AudioData audioData);
    }

    public record ConverterModel(object ModelType, string Name);
}

