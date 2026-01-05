using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using VoiceR.Config;
using VoiceR.Model;

namespace VoiceR.Llm
{

    /// <summary>
    /// Service for interacting with OpenAI's chat completion API.
    /// </summary>
    public class OpenAIService : ILlmService
    {
        private readonly string _systemPrompt;

        private string? _serializedContext = null;

        // from interface
        public LargeLanguageModel Model { get; set; }
        private Item? _scope = null;
        public Item? Scope
        {
            get => _scope;
            set
            {
                _scope = value;
                _serializedContext = null;
            }
        }

        // dependencies
        private readonly ConfigService _configService;
        private readonly AutomationService _automationService;
        private readonly ISerializer _serializer;
        private readonly IDeserializer _deserializer;
        private readonly ILogger _logger;


        public OpenAIService(ConfigService configService, AutomationService automationService, ISerializer serializer, IDeserializer deserializer, ILogger logger)
        {
            _configService = configService;
            _automationService = automationService;
            _serializer = serializer;
            _deserializer = deserializer;
            _logger = logger;

            Model = AvailableModels[0];

            _systemPrompt = configService.Load().SystemPrompt ?? string.Empty;
            _logger.LogInformation("System prompt loaded");

            _systemPrompt = $$"""
You are a helpful assistant that can help with UI automation in Windows. As context, you will get a {{_serializer.Format}} structure of the visual hierarchy of the UI. The hierarchy is a tree, so parent nodes give context for the children recursively.
Each item in the tree is a UI element and has several possible actions to execute. Those depend on "available patterns". Possible patterns and actions and params are:
- ExpandCollapse: Expand or collapse the element (e.g. menus or panes)
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

The user will ask you to perform an action on one ore more specific UI elements. You will need to determine the best way to perform the action based on the available patterns and the context. If you do not know what to do, return an empty list.
""";
        }

        public string SerializedContext
        {
            get
            {
                if (_serializedContext != null)
                {
                    return _serializedContext;
                }
                Item? item = Scope ?? _automationService.CompactRoot ?? _automationService.Root;
                if (item == null)
                {
                    throw new InvalidOperationException("Cannot retrieve scope");
                }
                _serializedContext = _serializer.Serialize(item);
                return _serializedContext;
            }
        }

        /// <summary>
        /// Available OpenAI models (Standard tier pricing).
        /// </summary>
        /// <see cref="https://platform.openai.com/docs/pricing?latest-pricing=standard"/>
        public static readonly IReadOnlyList<LargeLanguageModel> _availableModels = new List<LargeLanguageModel>
        {
            new LargeLanguageModel("gpt-5-mini", 0.25m, 2.00m),
            new LargeLanguageModel("gpt-5-nano", 0.05m, 0.40m),
            new LargeLanguageModel("gpt-5", 1.25m, 10.00m),
            new LargeLanguageModel("gpt-4.1-mini", 0.40m, 1.60m),
            new LargeLanguageModel("gpt-4.1-nano", 0.10m, 0.40m),
            new LargeLanguageModel("gpt-4.1", 2.00m, 8.00m),
            new LargeLanguageModel("gpt-4o-mini", 0.15m, 0.60m),
            new LargeLanguageModel("gpt-3.5-turbo", 0.50m, 1.50m),
        };

        public List<LargeLanguageModel> AvailableModels => _availableModels.ToList();

        /// <summary>
        /// Generates a response from OpenAI based on the user prompt.
        /// </summary>
        /// <param name="userPrompt">The user's prompt text.</param>
        public async Task<LlmResult> GenerateAsync(string userPrompt)
        {
            string _apiKey = _configService.Load().OpenAiApiKey ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogError("OpenAI API key is not configured.");
                throw new InvalidOperationException("OpenAI API key is not configured.");
            }
            ChatClient client = new ChatClient(Model.Name, _apiKey);

            // Combine context with user prompt
            string context = SerializedContext;
            string fullPrompt = string.IsNullOrWhiteSpace(context)
                ? userPrompt
                : $"Context (UI Element JSON):\n{context}\n\nUser Request:\n{userPrompt}";
            ChatMessage[] messages = new ChatMessage[]
            {
                new SystemChatMessage(_systemPrompt),
                new UserChatMessage(fullPrompt)
            };

            // call the LLM
            Stopwatch stopwatch = Stopwatch.StartNew();
            var completion = await client.CompleteChatAsync(messages);
            stopwatch.Stop();

            // prepare result
            ChatCompletion? content = completion.Value;
            LlmResult result = new LlmResult();
            result.Prompt = fullPrompt;
            result.Response = content.Content[0].Text;
            result.InputTokens = content.Usage.InputTokenCount;
            result.OutputTokens = content.Usage.OutputTokenCount;
            result.EstimatedInputPriceUSD = result.InputTokens * Model.InputPricePerMillion / 1000000;
            result.EstimatedOutputPriceUSD = result.OutputTokens * Model.OutputPricePerMillion / 1000000;
            result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            _deserializer.ExtractActionsFromResponse(result.Response, out List<Action> actions, out List<string> errors);
            result.Actions = actions;
            result.Errors = errors;

            return result;
        }
    }
}

