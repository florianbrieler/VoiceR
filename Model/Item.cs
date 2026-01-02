using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace VoiceR.Model
{
    /// <summary>
    /// Represents a node in the UI Automation tree hierarchy.
    /// </summary>
    public class Item
    {
        public string Id { get; init; } = GenerateShortGuid();

        public string ControlType { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string AutomationId { get; init; } = string.Empty;
        public string ClassName { get; init; } = string.Empty;
        public string HelpText { get; init; } = string.Empty;

        public HashSet<Pattern> AvailablePatterns { get; set; } = [];
        public HashSet<Property> Properties { get; set; } = [];

        public LevelOfInformation LoI { get; set; } = LevelOfInformation.Unknown;
        public required AutomationElement Element;

        /// <summary>
        /// Child nodes in the UI hierarchy.
        /// </summary>
        private List<Item> Children { get; } = [];

        public static Item FromElement(AutomationElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            ControlType controlType = element.Current.ControlType;
            // ControlType.ProgrammaticName returns "ControlType.Button" etc.
            string programmaticName = controlType.ProgrammaticName ?? string.Empty;
            programmaticName = programmaticName.Replace("ControlType.", "");

            object ht = element.GetCurrentPropertyValue(AutomationElement.HelpTextProperty, true);

            HashSet<Pattern> availablePatterns = [];
            foreach (Pattern pattern in Enum.GetValues(typeof(Pattern)))
            {
                if ((bool)element.GetCurrentPropertyValue(pattern.AsAutomationProperty()))
                {
                    availablePatterns.Add(pattern);
                }
            }

            HashSet<Property> props = [];
            foreach (Property property in Enum.GetValues(typeof(Property)))
            {
                if ((bool)element.GetCurrentPropertyValue(property.AsAutomationProperty()))
                {
                    props.Add(property);
                }
            }

            return new Item
            {
                ControlType = programmaticName,
                Name = element.Current.Name ?? string.Empty,
                AutomationId = element.Current.AutomationId ?? string.Empty,
                ClassName = element.Current.ClassName ?? string.Empty,
                HelpText = ht == AutomationElement.NotSupported ? string.Empty : (string) ht, // why not just element.current.helpText?
                AvailablePatterns = availablePatterns,
                Element = element,
                Properties = props,
            };
        }

        public static Item FromItem(Item item)
        {
            return new Item
            {
                Id = item.Id,
                ControlType = item.ControlType,
                Name = item.Name,
                AutomationId = item.AutomationId,
                ClassName = item.ClassName,
                HelpText = item.HelpText,
                AvailablePatterns = [.. item.AvailablePatterns],
                Properties = [.. item.Properties],
                LoI = item.LoI,
                Element = item.Element
            };
        }

        public void AddChild(Item child)
        {
            Children.Add(child);
        }

        public void AddChildren(IEnumerable<Item> children)
        {
            Children.AddRange(children);
        }

        public IEnumerable<Item> GetChildren()
        {
            return Children;
        }

        public bool IsPatternAvailable(Pattern pattern)
        {
            return AvailablePatterns.Contains(pattern);
        }

        public string DisplayText
        {
            get
            {
                var parts = new List<string>();

                // Always show control type (never empty)
                parts.Add(ControlType);

                List<string> details = [];
                if (!string.IsNullOrEmpty(Name))
                {
                    details.Add($"{Name}");
                }
                if (!string.IsNullOrEmpty(AutomationId))
                {
                    details.Add($"{AutomationId}");
                }
                if (!string.IsNullOrEmpty(ClassName))
                {
                    details.Add($"{ClassName}");
                }
                parts.Add($"| details: {(details.Count > 0 ? string.Join(", ", details) : "-")}");

                parts.Add($"| properties: {string.Join(", ", Properties)}");
                parts.Add($"| patterns: {(AvailablePatterns.Count > 0 ? string.Join(", ", AvailablePatterns) : "-")}");
                parts.Add($"| id: {Id}");

                return string.Join(" ", parts);
            }
        }

        public enum LevelOfInformation
        {
            Unknown,
            Full,
            Connector,
            None
        }

        public void CheckLevelOfInformation() {
            LevelOfInformation maxLoI = LevelOfInformation.None;

            foreach (Item child in Children)
            {
                child.CheckLevelOfInformation();
                if (child.LoI.CompareTo(maxLoI) < 0)
                {
                    maxLoI = child.LoI;
                }
            }

            if (!string.IsNullOrEmpty(Name) || AvailablePatterns.Count > 0) // ClassName as well?
            {
                LoI = LevelOfInformation.Full;
            } else if (maxLoI == LevelOfInformation.Full) {
                LoI = LevelOfInformation.Connector;
            } else {
                LoI = maxLoI;
            }
        }

        private static string GenerateShortGuid()
        {
            return Guid.NewGuid().ToString("N")[..12];
        }
    }

    public enum Pattern
    {
        Dock,
        ExpandCollapse,
        Grid,
        GridItem,
        Invoke,
        ItemContainer,
        MultipleView,
        RangeValue,
        ScrollItem,
        Scroll,
        SelectionItem,
        Selection,
        SyncInput,
        TableItem,
        Table,
        Text,
        Toggle,
        Transform,
        Value,
        VirtItem,
        Window
    }

    public static class PatternExtensions
    {
        public static bool IsPatternAvailable(this Pattern pattern, AutomationElement element)
        {
            return (bool)element.GetCurrentPropertyValue(pattern.AsAutomationProperty());
        }
        public static AutomationProperty AsAutomationProperty(this Pattern pattern)
        {
            return pattern switch
            {
                Pattern.Dock => AutomationElement.IsDockPatternAvailableProperty,
                Pattern.ExpandCollapse => AutomationElement.IsExpandCollapsePatternAvailableProperty,
                Pattern.Grid => AutomationElement.IsGridItemPatternAvailableProperty,
                Pattern.GridItem => AutomationElement.IsGridItemPatternAvailableProperty,
                Pattern.Invoke => AutomationElement.IsInvokePatternAvailableProperty,
                Pattern.ItemContainer => AutomationElement.IsItemContainerPatternAvailableProperty,
                Pattern.MultipleView => AutomationElement.IsMultipleViewPatternAvailableProperty,
                Pattern.RangeValue => AutomationElement.IsRangeValuePatternAvailableProperty,
                Pattern.ScrollItem => AutomationElement.IsScrollItemPatternAvailableProperty,
                Pattern.Scroll => AutomationElement.IsScrollPatternAvailableProperty,
                Pattern.SelectionItem => AutomationElement.IsSelectionItemPatternAvailableProperty,
                Pattern.Selection => AutomationElement.IsSelectionPatternAvailableProperty,
                Pattern.SyncInput => AutomationElement.IsSynchronizedInputPatternAvailableProperty,
                Pattern.TableItem => AutomationElement.IsTableItemPatternAvailableProperty,
                Pattern.Table => AutomationElement.IsTablePatternAvailableProperty,
                Pattern.Text => AutomationElement.IsTextPatternAvailableProperty,
                Pattern.Toggle => AutomationElement.IsTogglePatternAvailableProperty,
                Pattern.Transform => AutomationElement.IsTransformPatternAvailableProperty,
                Pattern.Value => AutomationElement.IsValuePatternAvailableProperty,
                Pattern.VirtItem => AutomationElement.IsVirtualizedItemPatternAvailableProperty,
                Pattern.Window => AutomationElement.IsWindowPatternAvailableProperty,
                _ => throw new ArgumentException($"Invalid pattern: {pattern}")
            };
        }
        public static AutomationPattern AsAutomationPattern(this Pattern pattern)
        {
            return pattern switch
            {
                Pattern.Dock => DockPattern.Pattern,
                Pattern.ExpandCollapse => ExpandCollapsePattern.Pattern,
                Pattern.Grid => GridItemPattern.Pattern,
                Pattern.GridItem => GridItemPattern.Pattern,
                Pattern.Invoke => InvokePattern.Pattern,
                Pattern.ItemContainer => ItemContainerPattern.Pattern,
                Pattern.MultipleView => MultipleViewPattern.Pattern,
                Pattern.RangeValue => RangeValuePattern.Pattern,
                Pattern.ScrollItem => ScrollItemPattern.Pattern,
                Pattern.Scroll => ScrollPattern.Pattern,
                Pattern.SelectionItem => SelectionItemPattern.Pattern,
                Pattern.Selection => SelectionPattern.Pattern,
                Pattern.SyncInput => SynchronizedInputPattern.Pattern,
                Pattern.TableItem => TableItemPattern.Pattern,
                Pattern.Table => TablePattern.Pattern,
                Pattern.Text => TextPattern.Pattern,
                Pattern.Toggle => TogglePattern.Pattern,
                Pattern.Transform => TransformPattern.Pattern,
                Pattern.Value => ValuePattern.Pattern,
                Pattern.VirtItem => VirtualizedItemPattern.Pattern,
                Pattern.Window => WindowPattern.Pattern,
                _ => throw new ArgumentException($"Invalid pattern: {pattern}")
            };
        }
    }

    public enum Property
    {
        Content,
        Control,
        Dialog,
        Enabled,
        Offscreen
    }

    public static class PropertyExtensions
    {
        public static bool IsPropertyAvailable(this Property property, AutomationElement element)
        {
            return (bool)element.GetCurrentPropertyValue(property.AsAutomationProperty());
        }
        public static AutomationProperty AsAutomationProperty(this Property property)
        {
            return property switch
            {
                Property.Content => AutomationElement.IsContentElementProperty,
                Property.Control => AutomationElement.IsControlElementProperty,
                Property.Dialog => AutomationElement.IsDialogProperty,
                Property.Enabled => AutomationElement.IsEnabledProperty,
                Property.Offscreen => AutomationElement.IsOffscreenProperty,
                _ => throw new ArgumentException($"Invalid property: {property}")
            };
        }
    }
}

