using System;
using System.Diagnostics;
using System.Windows.Automation;

namespace VoiceR
{
    /// <summary>
    /// Service for retrieving UI elements using Microsoft UI Automation.
    /// </summary>
    public static class UIAutomationService
    {

        public const int MaxDepth = 5;

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
                result.RootNode = UIAutomationTreeNode.fromError(ex.Message);
                result.TotalNodeCount = 1;
            }

            stopwatch.Stop();
            result.RetrievalTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }

        /// <summary>
        /// Recursively builds a tree node from an AutomationElement.
        /// </summary>
        private static UIAutomationTreeNode BuildTreeNode(AutomationElement element, ref int nodeCount, int depth=0)
        {
            nodeCount++;

            UIAutomationTreeNode node = UIAutomationTreeNode.fromElement(element);

            if (depth >= MaxDepth) {
                return node;
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
                        var childNode = BuildTreeNode(child, ref nodeCount, depth + 1);
                        node.AddChild(childNode);
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
    }
}

