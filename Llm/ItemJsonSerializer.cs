using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceR.Model;

namespace VoiceR.Llm
{
    /// <summary>
    /// Serializes Item objects to JSON representation for LLM consumption.
    /// </summary>
    public static class ItemJsonSerializer
    {
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Converts an Item to a JSON string representation.
        /// </summary>
        /// <param name="item">The Item to serialize.</param>
        /// <returns>A formatted JSON string.</returns>
        public static string ToJson(Item item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            var dto = ConvertToDto(item);
            return JsonSerializer.Serialize(dto, _jsonOptions);
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

