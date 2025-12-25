using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;

namespace VoiceR
{
    /// <summary>
    /// Service for retrieving UI elements using Microsoft UI Automation.
    /// </summary>
    public static class UIAutomationService
    {
        /// <summary>
        /// Result of a UI Automation scan.
        /// </summary>
        public class ScanResult
        {
            /// <summary>
            /// The root node of the UI tree.
            /// </summary>
            public UIAutomationTreeNode RootNode { get; set; } = new UIAutomationTreeNode();

            /// <summary>
            /// Time taken to retrieve all UI elements in milliseconds.
            /// </summary>
            public long RetrievalTimeMs { get; set; }

            /// <summary>
            /// Total count of all nodes (inner nodes + leaves).
            /// </summary>
            public int TotalNodeCount { get; set; }
        }

        /// <summary>
        /// Scans the entire Windows UI tree and returns the results.
        /// </summary>
        /// <returns>A ScanResult containing the tree, timing, and count.</returns>
        public static ScanResult ScanUITree()
        {
            var result = new ScanResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Get the desktop root element
                AutomationElement rootElement = AutomationElement.RootElement;

                // Build the tree recursively
                int nodeCount = 0;
                result.RootNode = BuildTreeNode(rootElement, ref nodeCount);
                result.TotalNodeCount = nodeCount;
            }
            catch (Exception ex)
            {
                // If something goes wrong, create a placeholder node
                result.RootNode = new UIAutomationTreeNode
                {
                    ControlType = "Error",
                    Name = ex.Message
                };
                result.TotalNodeCount = 1;
            }

            stopwatch.Stop();
            result.RetrievalTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }

        /// <summary>
        /// Recursively builds a tree node from an AutomationElement.
        /// </summary>
        private static UIAutomationTreeNode BuildTreeNode(AutomationElement element, ref int nodeCount)
        {
            nodeCount++;

            var node = new UIAutomationTreeNode();

            try
            {
                // Get element properties
                node.ControlType = GetControlTypeName(element);
                node.Name = element.Current.Name ?? string.Empty;
                node.AutomationId = element.Current.AutomationId ?? string.Empty;
                node.ClassName = element.Current.ClassName ?? string.Empty;
            }
            catch
            {
                // Element properties may throw if element becomes invalid
                node.ControlType = "Unknown";
            }

            try
            {
                // Use ControlViewWalker for a cleaner view of the UI tree
                TreeWalker walker = TreeWalker.ControlViewWalker;
                AutomationElement? child = walker.GetFirstChild(element);

                while (child != null)
                {
                    try
                    {
                        var childNode = BuildTreeNode(child, ref nodeCount);
                        node.Children.Add(childNode);
                        child = walker.GetNextSibling(child);
                    }
                    catch
                    {
                        // Skip problematic children
                        try
                        {
                            child = walker.GetNextSibling(child);
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors when walking children
            }

            return node;
        }

        /// <summary>
        /// Gets the control type name from an AutomationElement.
        /// </summary>
        private static string GetControlTypeName(AutomationElement element)
        {
            try
            {
                ControlType controlType = element.Current.ControlType;
                // ControlType.ProgrammaticName returns "ControlType.Button" etc.
                string name = controlType.ProgrammaticName;
                // Remove the "ControlType." prefix
                if (name.StartsWith("ControlType."))
                {
                    return name.Substring(12);
                }
                return name;
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}

