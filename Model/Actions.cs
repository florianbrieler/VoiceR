using System;
using System.Windows.Automation;

namespace VoiceR.Model
{
    public static class Actions
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

        public static Action CreateActionFromStrings(this Item item, string action, string[] parameters)
        {
            switch (action.ToLower())
            {
                case "expandorcollapse":
                    CheckIfPatternIsAvailable(item, Pattern.ExpandCollapse);
                    CheckIfParametersAreValid(action, parameters, 1);
                    ExpandCollapseState staEC = GetExpandCollapseState(parameters[0], action);
                    return () => { item.ExpandOrCollapse(staEC); };
                case "invoke":
                    CheckIfPatternIsAvailable(item, Pattern.Invoke);
                    CheckIfParametersAreValid(action, parameters, 0);
                    return () => { item.Invoke(); };
                case "toggle":
                    CheckIfPatternIsAvailable(item, Pattern.Toggle);
                    CheckIfParametersAreValid(action, parameters, 0);
                    return () => { item.Toggle(); };
                case "arrange":
                    CheckIfPatternIsAvailable(item, Pattern.Transform);
                    CheckIfParametersAreValid(action, parameters, 1);
                    ArrangeState staAr = GetArrangeState(parameters[0], action);
                    return () => { item.Arrange(staAr); };
                case "setvalue":
                    CheckIfPatternIsAvailable(item, Pattern.Value);
                    CheckIfParametersAreValid(action, parameters, 1);
                    return () => { item.SetValue(parameters[0]); };
                case "setwindowvisualstate":
                    CheckIfPatternIsAvailable(item, Pattern.Window);
                    CheckIfParametersAreValid(action, parameters, 1);
                    WindowVisualState staWin = GetWindowVisualState(parameters[0], action);
                    return () => { item.SetWindowVisualState(staWin); };
                case "closewindow":
                    CheckIfPatternIsAvailable(item, Pattern.Window);
                    CheckIfParametersAreValid(action, parameters, 0);
                    return () => { item.CloseWindow(); };
                default:
                    throw new InvalidOperationException($"Invalid action: {action}");
            }
        }

        private static void CheckIfPatternIsAvailable(Item item, Pattern pattern)
        {
            if (!item.AvailablePatterns.Contains(pattern))
            {
                throw new InvalidOperationException($"{pattern} is not available for item");
            }
        }

        private static void CheckIfParametersAreValid(string action, string[] parameters, int requiredParameters)
        {
            if (parameters.Length != requiredParameters)
            {
                throw new InvalidOperationException($"{action} requires {requiredParameters} parameters, but {parameters.Length} were provided");
            }
        }

        private static ExpandCollapseState GetExpandCollapseState(string str, String action)
        {
            switch (str.ToLower())
            {
                case "expanded":
                    return ExpandCollapseState.Expanded;
                case "collapsed":
                    return ExpandCollapseState.Collapsed;
                default:
                    throw new InvalidOperationException($"Invalid parameter for {action}: {str}");
            }
        }

        private static ArrangeState GetArrangeState(string str, String action)
        {
            switch (str.ToLower())
            {
                case "left":
                    return ArrangeState.Left;
                case "right":
                    return ArrangeState.Right;
                case "top":
                    return ArrangeState.Top;
                case "bottom":
                    return ArrangeState.Bottom;
                case "center":
                    return ArrangeState.Center;
                default:
                    throw new InvalidOperationException($"Invalid parameter for {action}: {str}");
            }
        }

        private static WindowVisualState GetWindowVisualState(string str, String action)
        {
            switch (str.ToLower())
            {
                case "maximized":
                    return WindowVisualState.Maximized;
                case "minimized":
                    return WindowVisualState.Minimized;
                case "normal":
                    return WindowVisualState.Normal;
                default:
                    throw new InvalidOperationException($"Invalid parameter for {action}: {str}");
            }
        }
    }
}

