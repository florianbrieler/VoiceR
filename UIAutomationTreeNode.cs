using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Automation;

namespace VoiceR
{
    /// <summary>
    /// Represents a node in the UI Automation tree hierarchy.
    /// </summary>
    public class UIAutomationTreeNode
    {
        private string ControlType { get; init; } = string.Empty;
        private string Name { get; init; } = string.Empty;
        private string AutomationId { get; init; } = string.Empty;
        private string ClassName { get; init; } = string.Empty;
        private bool IsOffscreen { get; init; } = false;
        public bool IsWindowPatternAvailable { get; init; } = false;

        private bool IsError { get; init; } = false;

        private AutomationElement? Element { get; init; }

        /// <summary>
        /// Child nodes in the UI hierarchy.
        /// </summary>
        private List<UIAutomationTreeNode> Children { get; set; } = new List<UIAutomationTreeNode>();

        public static UIAutomationTreeNode fromElement(AutomationElement element)
        {
            ControlType controlType = element.Current.ControlType;
            // ControlType.ProgrammaticName returns "ControlType.Button" etc.

            return new UIAutomationTreeNode
            {
                ControlType = controlType.ProgrammaticName ?? string.Empty,
                Name = element.Current.Name ?? string.Empty,
                AutomationId = element.Current.AutomationId ?? string.Empty,
                ClassName = element.Current.ClassName ?? string.Empty,
                IsOffscreen = element.Current.IsOffscreen,
                IsWindowPatternAvailable = (bool) element.GetCurrentPropertyValue(AutomationElement.IsWindowPatternAvailableProperty),
                Element = element,
            };
        }

        public static UIAutomationTreeNode fromError(string errorMessage) {
            return new UIAutomationTreeNode
            {
                ControlType = "Error",
                Name = errorMessage,
                IsError = true,
            };

        }

        private WindowPattern? GetWindowPattern()
        {
            if (Element == null || !IsWindowPatternAvailable) {
                return null;
            }

            WindowPattern? windowPattern = null;
            try
            {
                windowPattern = Element.GetCurrentPattern(WindowPattern.Pattern) as WindowPattern;
            }
            catch (Exception ex) {
                // write to console
                Console.WriteLine($"Error getting window pattern: {ex.Message}");
            }
            if (windowPattern == null) {
                return null;
            }

            // Make sure the element is usable.
            if (false == windowPattern.WaitForInputIdle(10000))
            {
                // Object not responding in a timely manner
                Console.WriteLine($"Element not responding in a timely manner");
                return null;
            }
            return windowPattern;
        }

        public void SetWindowVisualState(WindowVisualState visualState)
        {
            Console.WriteLine($"Setting window state of {DisplayText} to {visualState}");

            WindowPattern? windowPattern = GetWindowPattern();
            if (windowPattern == null) {
                Console.WriteLine($"Could not get window pattern");
                return;
            }
            
            try
            {
                if (windowPattern.Current.WindowInteractionState ==  WindowInteractionState.ReadyForUserInteraction)
                {
                    switch (visualState)
                    {
                        case WindowVisualState.Maximized:
                            // Confirm that the element can be maximized
                            if (windowPattern.Current.CanMaximize &&  !windowPattern.Current.IsModal)
                            {
                                windowPattern.SetWindowVisualState(WindowVisualState.Maximized);
                            }
                            break;
                        case WindowVisualState.Minimized:
                            // Confirm that the element can be minimized
                            if (windowPattern.Current.CanMinimize && !windowPattern.Current.IsModal)
                            {
                                windowPattern.SetWindowVisualState(WindowVisualState.Minimized);
                            }
                            break;
                        case WindowVisualState.Normal:
                        default:
                            windowPattern.SetWindowVisualState(WindowVisualState.Normal);
                            break;
                    }
                }
            }
            catch (Exception ex){
                // write to console
                Console.WriteLine($"Error setting window visual state: {ex.Message}");
            }
            
            Console.WriteLine($"Window state of {DisplayText} successfully set to {visualState}");
        }

        public void AddChild(UIAutomationTreeNode child)
        {
            Children.Add(child);
        }

        public IEnumerable<UIAutomationTreeNode> GetChildren()
        {
            return Children;
        }

        public string DisplayText
        {
            get
            {
                var parts = new List<string>();
                
                // Always show control type
                parts.Add(ControlType);
                
                // Add name if present
                if (!string.IsNullOrEmpty(Name))
                {
                    parts[0] = $"{ControlType}: {Name}";
                }
                
                if (!IsError)
                {
                    // Add automation ID if present
                    if (!string.IsNullOrEmpty(AutomationId))
                    {
                        parts.Add($"[{AutomationId}]");
                    }
                    
                    // Add class name if present
                    if (!string.IsNullOrEmpty(ClassName))
                    {
                        parts.Add($"({ClassName})");
                    }

                    parts.Add($"| offscreen: {IsOffscreen}");
                    parts.Add($"| windowpatternavailable: {IsWindowPatternAvailable}");
                }

                return string.Join(" ", parts);
            }
        }
    }
}

