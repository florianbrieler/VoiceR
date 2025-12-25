using System.Collections.Generic;

namespace VoiceR
{
    /// <summary>
    /// Represents a node in the UI Automation tree hierarchy.
    /// </summary>
    public class UIAutomationTreeNode
    {
        /// <summary>
        /// The control type of the UI element (e.g., "Button", "Window", "Edit").
        /// </summary>
        public string ControlType { get; set; } = string.Empty;

        /// <summary>
        /// The name/label of the UI element.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The automation ID of the UI element.
        /// </summary>
        public string AutomationId { get; set; } = string.Empty;

        /// <summary>
        /// The class name of the UI element.
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// Child nodes in the UI hierarchy.
        /// </summary>
        public List<UIAutomationTreeNode> Children { get; set; } = new List<UIAutomationTreeNode>();

        /// <summary>
        /// Gets a display string for this node.
        /// Format: "{ControlType}: {Name} [{AutomationId}] ({ClassName})"
        /// </summary>
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
                
                return string.Join(" ", parts);
            }
        }
    }
}

