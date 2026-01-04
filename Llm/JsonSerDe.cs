using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceR.Model;

namespace VoiceR.Llm
{
    public class JsonSerDe : ISerializer, IDeserializer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private AutomationService _automationService;

        public JsonSerDe(AutomationService automationService)
        {
            _automationService = automationService;
        }

        public string Format => "json";

        public string Serialize(Item item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var dto = ConvertToDto(item);
            return JsonSerializer.Serialize(dto, _jsonOptions);
        }

        public void ExtractActionsFromResponse(string response, out List<Action> actions, out List<string> errors)
        {
            JsonElement root;
            JsonElement actionsElement;

            actions = [];
            errors = [];

            // check if response is empty
            if (string.IsNullOrWhiteSpace(response))
            {
                errors.Add("Response is empty");
                return;
            }

            try
            {
                // parse response as Json
                using JsonDocument doc = JsonDocument.Parse(response);
                root = doc.RootElement;

                // expect object root with "actions" property that is an array
                try
                {
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add("the response is not an object");
                        return;
                    }

                    if (!root.TryGetProperty("actions", out actionsElement))
                    {
                        errors.Add("there is no actions property in the response");
                        return;
                    }

                    if (actionsElement.ValueKind != JsonValueKind.Array)
                    {
                        errors.Add("actions is not an array");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error extracting actions from response: {ex.Message}");
                    return;
                }

                // check the actions and convert
                int index = 0;
                foreach (JsonElement child in actionsElement.EnumerateArray())
                {
                    if (child.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"action #{index}: is not an object");
                        continue;
                    }

                    string id;
                    string actionString;
                    List<string> parameters = [];
                    JsonElement paramsElement;

                    // get id
                    try
                    {
                        string? s = child.GetProperty("id").GetString();
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            errors.Add($"action #{index}: id is empty");
                            continue;
                        }
                        id = s;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"action #{index}: cannot retrieve id: {ex.Message}");
                        continue;
                    }

                    // get action
                    try
                    {
                        string? s = child.GetProperty("action").GetString();
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            errors.Add($"action #{index}: action is empty");
                            continue;
                        }
                        actionString = s;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"action #{index}: cannot retrieve action: {ex.Message}");
                        continue;
                    }

                    // get params
                    if (child.TryGetProperty("params", out paramsElement))
                    {
                        try
                        {
                            if (paramsElement.ValueKind != JsonValueKind.Array)
                            {
                                errors.Add($"action #{index}: params is not an array");
                                continue;
                            }
                            foreach (JsonElement param in paramsElement.EnumerateArray())
                            {
                                if (param.ValueKind != JsonValueKind.String)
                                {
                                    errors.Add($"action #{index}: param is not a string");
                                    continue;
                                }
                                string? s = param.GetString();
                                if (string.IsNullOrWhiteSpace(s))
                                {
                                    errors.Add($"action #{index}: param has no value");
                                    continue;
                                }
                                parameters.Add(s);
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"action #{index}: cannot retrieve action: {ex.Message}");
                            continue;
                        }
                    }

                    // create action
                    try
                    {
                        Item item = _automationService.GetItemForId(id);
                        Action action = item.CreateActionFromStrings(actionString, parameters.ToArray());
                        actions.Add(action);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"action #{index}: {ex.Message}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error parsing response as Json: {ex.Message}");
                return;
            }
        }

        private static ItemDto ConvertToDto(Item item)
        {
            var id = item.Id;
            var controlType = item.ControlType as string ?? string.Empty;
            var name = item.Name as string ?? string.Empty;
            var automationId = item.AutomationId as string ?? string.Empty;
            var className = item.ClassName as string ?? string.Empty;
            var patterns = item.AvailablePatterns?.Select(p => p.ToString()).ToList();
            var properties = item.Properties?.Select(p => p.ToString()).ToList();
            var children = item.GetChildren().Select(ConvertToDto).ToList();

            return new ItemDto
            {
                Id = id,
                ControlType = string.IsNullOrEmpty(controlType) ? null : controlType,
                Name = string.IsNullOrEmpty(name) ? null : name,
                AutomationId = string.IsNullOrEmpty(automationId) ? null : automationId,
                ClassName = string.IsNullOrEmpty(className) ? null : className,
                AvailablePatterns = patterns?.Count > 0 ? patterns : null,
                Properties = properties?.Count > 0 ? properties : null,
                Children = children.Count > 0 ? children : null
            };
        }

        /// <summary>
        /// Data Transfer Object for Item serialization.
        /// Excludes the AutomationElement reference and other internal state.
        /// </summary>
        private class ItemDto
        {
            public string Id { get; set; } = "tbd";
            public string? ControlType { get; set; }
            public string? Name { get; set; }
            public string? AutomationId { get; set; }
            public string? ClassName { get; set; }
            public List<string>? AvailablePatterns { get; set; }
            public List<string>? Properties { get; set; }
            public List<ItemDto>? Children { get; set; }
        }
    }
}

