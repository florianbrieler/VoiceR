using System;
using System.Collections.Generic;
using System.Linq;
using VoiceR.Model;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;
using YamlDotNet.Serialization.NamingConventions;

namespace VoiceR.Llm
{
    /// <summary>
    /// Serializes Item objects to YAML representation for LLM consumption.
    /// </summary>
    public class YamlSerializer : ISerializer
    {
        public bool UseCompression { get; set; } = false;

        public YamlSerializer() : this(false)
        {
        }

        public YamlSerializer(bool useCompression)
        {
            UseCompression = useCompression;
        }

        public string Format => "yaml";

        /// <summary>
        /// Converts an Item to a YAML string representation.
        /// </summary>
        /// <param name="item">The Item to serialize.</param>
        /// <returns>A formatted YAML string.</returns>
        public string Serialize(Item item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            string yamlOutput;

            if (UseCompression)
            {
                var dto = ConvertToCompressedDto(item);
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .WithEventEmitter(nextEmitter => new FlowStyleStringSequences(nextEmitter))
                    .Build();
                yamlOutput = serializer.Serialize(dto);

                // Prepend legend
                var legend = "# Legend: i=Id, ct=ControlType, n=Name, ai=AutomationId, cn=ClassName, ap=AvailablePatterns, p=Properties, c=Children\n";
                return legend + yamlOutput;
            }
            else
            {
                var dto = ConvertToDto(item);
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .WithEventEmitter(nextEmitter => new FlowStyleStringSequences(nextEmitter))
                    .Build();
                yamlOutput = serializer.Serialize(dto);
                return yamlOutput;
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

        private static CompressedItemDto ConvertToCompressedDto(Item item)
        {
            var id = item.Id;
            var controlType = item.ControlType as string ?? string.Empty;
            var name = item.Name as string ?? string.Empty;
            var automationId = item.AutomationId as string ?? string.Empty;
            var className = item.ClassName as string ?? string.Empty;
            var patterns = item.AvailablePatterns?.Select(p => p.ToString()).ToList();
            var properties = item.Properties?.Select(p => p.ToString()).ToList();
            var children = item.GetChildren().Select(ConvertToCompressedDto).ToList();

            return new CompressedItemDto
            {
                i = id,
                ct = string.IsNullOrEmpty(controlType) ? null : controlType,
                n = string.IsNullOrEmpty(name) ? null : name,
                ai = string.IsNullOrEmpty(automationId) ? null : automationId,
                cn = string.IsNullOrEmpty(className) ? null : className,
                ap = patterns?.Count > 0 ? patterns : null,
                p = properties?.Count > 0 ? properties : null,
                c = children.Count > 0 ? children : null
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

        /// <summary>
        /// Compressed Data Transfer Object for Item serialization with shortened property names.
        /// </summary>
        private class CompressedItemDto
        {
            public string i { get; set; } = "tbd";
            public string? ct { get; set; }
            public string? n { get; set; }
            public string? ai { get; set; }
            public string? cn { get; set; }
            public List<string>? ap { get; set; }
            public List<string>? p { get; set; }
            public List<CompressedItemDto>? c { get; set; }
        }

        private class FlowStyleStringSequences : ChainedEventEmitter
        {
            public FlowStyleStringSequences(IEventEmitter nextEmitter) : base(nextEmitter) { }

            public override void Emit(SequenceStartEventInfo eventInfo, IEmitter emitter)
            {
                if (eventInfo.Source.Type != null &&
                    eventInfo.Source.Type.IsGenericType &&
                    eventInfo.Source.Type.IsAssignableFrom(typeof(List<string>)) &&
                    eventInfo.Source.Type.GetGenericTypeDefinition() == typeof(List<>) &&
                    eventInfo.Source.Type.GetGenericArguments()[0] == typeof(string))
                {
                    eventInfo = new SequenceStartEventInfo(eventInfo.Source)
                    {
                        Style = SequenceStyle.Flow
                    };
                }
                base.Emit(eventInfo, emitter);
            }
        }
    }
}

