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
        /// <see cref="https://platform.openai.com/docs/pricing?latest-pricing=standard"/>
        public static readonly IReadOnlyList<OpenAIModel> AvailableModels = new List<OpenAIModel>
        {
            new OpenAIModel("gpt-5-mini", 0.25m, 2.00m),
            new OpenAIModel("gpt-5-nano", 0.05m, 0.40m),
            new OpenAIModel("gpt-5", 1.25m, 10.00m),
            new OpenAIModel("gpt-4.1-mini", 0.40m, 1.60m),
            new OpenAIModel("gpt-4.1-nano", 0.10m, 0.40m),
            new OpenAIModel("gpt-4.1", 2.00m, 8.00m),
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

            _systemPrompt = """
You are a helpful assistant that can help with UI automation in Windows. As a context, you will get a json structure of the visual hierarchy of the UI.
Each item in the json structure is a UI element and has several possible actions to execute. Those depend on "available patterns". Possible patterns and actions and params are:
- ExpandCollapse: Expand or collapse the element.
  Action: ExpandOrCollapse (possible params: expanded, collapsed)
- Invoke: Invoke the element like pressing a button.
  Action: Invoke (possible params: none)
- Toggle: Toggle the element like a checkbox.
  Action: Toggle (possible params: none)
- Transform: Transform the element like moving or resizing it.
  Action: Arrange (possible params: left, right, top, bottom, center)
- Value: Set the value of the element like a text box.
  Action: SetValue (possible params: value)
- Window: Anything you can do with a window.
  Action: SetWindowVisualState (possible params: maximized, minimized, normal)
  Action: CloseWindow (possible params: none)

Do not use any action that is not available for the element. Do not use any other parameters than the ones specified. Al parameters are mandatory.

The user will ask you to perform an action on one ore more specific UI elements. You will need to determine the best way to perform the action based on the available patterns and the context.

You will return a json object with the actions to perform on the UI elements and the possible parameters. The resulting json must be a list where each item is a json object with the action and the possible parameters.
{
    "actions": [
        {
            "id": "123",
            "action": "ExpandOrCollapse",
            "params": ["expanded"]
        }
    ]
}
""";
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

