using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoiceR.Model;

namespace VoiceR.Llm
{
    public interface ILlmService
    {
        Item? Scope { get; set; }
        string SerializedContext { get; }
        List<LargeLanguageModel> AvailableModels { get; }
        LargeLanguageModel Model { get; set; }
        Task<LlmResult> GenerateAsync(string prompt);
    }

    /// <summary>
    /// Represents an LLM with its pricing information.
    /// </summary>
    /// <param name="Name">The model identifier.</param>
    /// <param name="InputPricePerMillion">Input price per 1M tokens in USD.</param>
    /// <param name="OutputPricePerMillion">Output price per 1M tokens in USD.</param>
    public record LargeLanguageModel(string Name, decimal InputPricePerMillion, decimal OutputPricePerMillion);

    public class LlmResult {
        public string Prompt { get; set; }  = string.Empty;
        public string Response { get; set; } = string.Empty;
        public int InputTokens { get; set; } = 0;
        public int OutputTokens { get; set; } = 0;
        public decimal EstimatedInputPriceUSD { get; set; } = 0;
        public decimal EstimatedOutputPriceUSD { get; set; } = 0;
        public long ElapsedMilliseconds { get; set; } = 0;
        public List<Action> Actions { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }
}