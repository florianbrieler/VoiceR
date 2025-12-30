using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Chat;

namespace VoiceR.Llm
{
    /// <summary>
    /// Represents an OpenAI model with its pricing information.
    /// </summary>
    /// <param name="Name">The model identifier.</param>
    /// <param name="InputPricePerMillion">Input price per 1M tokens in USD.</param>
    /// <param name="OutputPricePerMillion">Output price per 1M tokens in USD.</param>
    public record OpenAIModel(string Name, decimal InputPricePerMillion, decimal OutputPricePerMillion);

    /// <summary>
    /// Service for interacting with OpenAI's chat completion API.
    /// </summary>
    public class OpenAIService
    {
        private readonly string _apiKey;
        private readonly string _systemPrompt;

        /// <summary>
        /// Available OpenAI models with input price less than $1.00 per 1M tokens (Standard tier pricing).
        /// </summary>
        public static readonly IReadOnlyList<OpenAIModel> AvailableModels = new List<OpenAIModel>
        {
            new OpenAIModel("gpt-5-mini", 0.25m, 2.00m),
            new OpenAIModel("gpt-5-nano", 0.05m, 0.40m),
            new OpenAIModel("gpt-4.1-mini", 0.40m, 1.60m),
            new OpenAIModel("gpt-4.1-nano", 0.10m, 0.40m),
            new OpenAIModel("gpt-4o-mini", 0.15m, 0.60m),
            new OpenAIModel("gpt-3.5-turbo", 0.50m, 1.50m),
        };

        /// <summary>
        /// Creates a new instance of the OpenAIService.
        /// </summary>
        /// <param name="apiKey">The OpenAI API key.</param>
        /// <param name="systemPrompt">The system prompt to use for all requests.</param>
        public OpenAIService(string apiKey, string systemPrompt)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _systemPrompt = systemPrompt ?? string.Empty;
        }

        /// <summary>
        /// Generates a response from OpenAI based on the user prompt.
        /// </summary>
        /// <param name="userPrompt">The user's prompt text.</param>
        /// <param name="model">The OpenAI model to use (e.g., "gpt-4o", "gpt-4o-mini").</param>
        /// <returns>The generated response text.</returns>
        public async Task<string> GenerateAsync(string userPrompt, OpenAIModel model)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured.");
            }

            var client = new ChatClient(model.Name, _apiKey);

            var messages = new ChatMessage[]
            {
                new SystemChatMessage(_systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages);

            return completion.Value.Content[0].Text;
        }
    }
}

