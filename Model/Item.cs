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

                // parts.Add(LoI.ToString());

                // Always show control type (never empty)
                parts.Add(ControlType);

                List<string> details = [];
                if (!string.IsNullOrEmpty(Name))
                {
                    details.Add($"name: {Name}");
                }
                if (!string.IsNullOrEmpty(AutomationId))
                {
                    details.Add($"automation id: {AutomationId}");
                }
                if (!string.IsNullOrEmpty(ClassName))
                {
                    details.Add($"class name: {ClassName}");
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

    public static class ItemActions
    {
        /// <summary>
        /// Set the expand/collapse state of the menu item.
        /// </summary>
        /// <param name="item">The item to set the expand/collapse state of.</param>
        /// <param name="expandCollapseState">The expand/collapse state to set.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.expandcollapsepattern"/>
        public static void ExpandOrCollapse(this Item item, ExpandCollapseState expandCollapseState)
        {
            Console.WriteLine($"Setting expand/collapse state of {item.DisplayText} to {expandCollapseState}");

            ExpandCollapsePattern? pattern = item.GetPattern<ExpandCollapsePattern>(Pattern.ExpandCollapse);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get expand/collapse pattern");
                return;
            }

            if (pattern.Current.ExpandCollapseState == ExpandCollapseState.LeafNode)
            {
                Console.WriteLine($"Not changing expand/collapse state of {item.DisplayText} because it is a leaf node");
                return;
            }

            try
            {
                switch (expandCollapseState)
                {
                    case ExpandCollapseState.Expanded:
                        if (pattern.Current.ExpandCollapseState != ExpandCollapseState.Expanded)
                        {
                            pattern.Expand();
                        }
                        break;
                    case ExpandCollapseState.Collapsed:
                        if (pattern.Current.ExpandCollapseState != ExpandCollapseState.Collapsed)
                        {
                            pattern.Collapse();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error setting expand/collapse state: {ex.Message}");
            }

            Console.WriteLine($"Expand/collapse state of {item.DisplayText} successfully set to {expandCollapseState}");
        }

        /// <summary>
        /// Invoke the item.
        /// </summary>
        /// <param name="item">The item to invoke.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.invokepattern"/>
        public static void Invoke(this Item item)
        {
            Console.WriteLine($"Invoking {item.DisplayText}");

            InvokePattern? pattern = item.GetPattern<InvokePattern>(Pattern.Invoke);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get invoke pattern");
                return;
            }

            try
            {
                pattern.Invoke();
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error invoking item: {ex.Message}");
            }

            Console.WriteLine($"Invoked {item.DisplayText} successfully");
        }

        /// <summary>
        /// Toggle the state of the element.
        /// </summary>
        /// <param name="item">The item to toggle.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.togglepattern"/>
        public static void Toggle(this Item item)
        {
            Console.WriteLine($"Toggling {item.DisplayText}");

            TogglePattern? pattern = item.GetPattern<TogglePattern>(Pattern.Toggle);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get toggle pattern");
                return;
            }

            try
            {
                pattern.Toggle();
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error toggling: {ex.Message}");
            }

            Console.WriteLine($"Toggled {item.DisplayText} successfully");
        }

        public enum ArrangeState
        {
            Left,
            Right,
            Top,
            Bottom,
            Center,
        }

        public static void Arrange(this Item item, ArrangeState arrangeState)
        {
            Console.WriteLine($"Arranging {item.DisplayText} to {arrangeState}");

            TransformPattern? pattern = item.GetPattern<TransformPattern>(Pattern.Transform);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get transform pattern");
                return;
            }

            if (!pattern.Current.CanMove || !pattern.Current.CanResize)
            {
                Console.WriteLine($"Cannot move or resize {item.DisplayText}");
                return;
            }

            try
            {
                // Get the width and height of the primary desktop screen
                double screenWidth = System.Windows.SystemParameters.PrimaryScreenWidth;
                double screenHeight = System.Windows.SystemParameters.PrimaryScreenHeight;

                switch (arrangeState) {
                    case ArrangeState.Left:
                        pattern.Move(0, 0);
                        pattern.Resize(screenWidth/2, screenHeight);
                        break;
                    case ArrangeState.Right:
                        pattern.Move(screenWidth/2, 0);
                        pattern.Resize(screenWidth/2, screenHeight);
                        break;
                    case ArrangeState.Top:
                        pattern.Move(0, 0);
                        pattern.Resize(screenWidth, screenHeight/2);
                        break;
                    case ArrangeState.Bottom:
                        pattern.Move(0, screenHeight/2);
                        pattern.Resize(screenWidth, screenHeight/2);
                        break;
                    case ArrangeState.Center:
                        pattern.Move(50, 50);
                        break;
                    default:
                        Console.WriteLine($"Invalid arrange state: {arrangeState}");
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error arranging: {ex.Message}");
            }

            Console.WriteLine($"Arranged {item.DisplayText} successfully to {arrangeState}");
        }

        /// <summary>
        /// Set the value of the element.
        /// </summary>
        /// <param name="item">The item to set the value of.</param>
        /// <param name="value">The value to set.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.valuepattern"/>
        public static void SetValue(this Item item, String value)
        {
            Console.WriteLine($"Setting value of {item.DisplayText} to {value}");

            ValuePattern? pattern = item.GetPattern<ValuePattern>(Pattern.Value);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get value pattern");
                return;
            }

            try
            {
                // this can go wrong for many reasons (disabled, readonly, focus, etc.)
                // we just attempt and let the exception handler deal with it
                pattern.SetValue(value);
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error setting value: {ex.Message}");
            }

            Console.WriteLine($"Set value of {item.DisplayText} successfully to {value}");
        }

        /// <summary>
        /// Set the window visual state of the element.
        /// </summary>
        /// <param name="visualState">The window visual state to set.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.windowpattern"/>
        public static void SetWindowVisualState(this Item item, WindowVisualState visualState)
        {
            Console.WriteLine($"Setting window state of {item.DisplayText} to {visualState}");

            WindowPattern? pattern = item.GetPattern<WindowPattern>(Pattern.Window);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get window pattern");
                return;
            }

            try
            {
                if (!pattern.WaitForInputIdle(10000))
                {
                    Console.WriteLine("Element not responding in a timely manner");
                    return;
                }

                if (pattern.Current.WindowInteractionState == WindowInteractionState.ReadyForUserInteraction)
                {
                    switch (visualState)
                    {
                        case WindowVisualState.Maximized:
                            // Confirm that the element can be maximized
                            if (pattern.Current.CanMaximize && !pattern.Current.IsModal)
                            {
                                pattern.SetWindowVisualState(WindowVisualState.Maximized);
                            }
                            break;
                        case WindowVisualState.Minimized:
                            // Confirm that the element can be minimized
                            if (pattern.Current.CanMinimize && !pattern.Current.IsModal)
                            {
                                pattern.SetWindowVisualState(WindowVisualState.Minimized);
                            }
                            break;
                        case WindowVisualState.Normal:
                        default:
                            pattern.SetWindowVisualState(WindowVisualState.Normal);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error setting window visual state: {ex.Message}");
            }

            Console.WriteLine($"Window state of {item.DisplayText} successfully set to {visualState}");
        }

        /// <summary>
        /// Close the window.
        /// </summary>
        /// <param name="item">The item to close.</param>
        /// <see cref="https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.windowpattern"/>
        public static void CloseWindow(this Item item)
        {
            Console.WriteLine($"Closing {item.DisplayText}");

            WindowPattern? pattern = item.GetPattern<WindowPattern>(Pattern.Window);
            if (pattern == null)
            {
                Console.WriteLine($"Could not get window pattern");
                return;
            }

            try
            {
                if (!pattern.WaitForInputIdle(10000))
                {
                    Console.WriteLine("Element not responding in a timely manner");
                    return;
                }

                pattern.Close();
            }
            catch (Exception ex)
            {
                // write to console
                Console.WriteLine($"Error closing window: {ex.Message}");
            }

            Console.WriteLine($"Closed window {item.DisplayText} successfully");
        }

        private static T? GetPattern<T>(this Item item, Pattern automationPattern) where T : BasePattern // class?
        {
            // Basic safety checks
            if (item.Element == null || !item.IsPatternAvailable(automationPattern))
            {
                return null;
            }

            // Get the pattern and cast it using 'as'
            T? pattern = null;
            try
            {
                pattern = item.Element.GetCurrentPattern(automationPattern.AsAutomationPattern()) as T;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting {typeof(T).Name}: {ex.Message}");
            }

            return pattern;
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

