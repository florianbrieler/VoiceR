using System;
using System.Diagnostics;
using System.Windows.Automation;

namespace VoiceR
{
    /// <summary>
    /// Service for retrieving UI elements using Microsoft UI Automation.
    /// </summary>
    public static class ItemService
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
            public Item RootNode { get; set; } = new Item();

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
                result.RootNode = Item.FromError(ex.Message);
                result.TotalNodeCount = 1;
            }

            stopwatch.Stop();
            result.RetrievalTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }

        /// <summary>
        /// Recursively builds a tree node from an AutomationElement.
        /// </summary>
        private static Item BuildTreeNode(AutomationElement element, ref int nodeCount, int depth=0)
        {
            nodeCount++;

            Item node = Item.FromElement(element);

            if (depth >= MaxDepth) {
                return node;
            }

            try
            {
                // Use TrueCondition to retrieve all elements.
                // AutomationElementCollection elementCollectionAll = elementMainWindow.FindAll(
                //     TreeScope.Subtree, Condition.TrueCondition);
                // Console.WriteLine("\nAll control types:");
                // foreach (AutomationElement autoElement in elementCollectionAll)
                // {
                //     Console.WriteLine(autoElement.Current.Name);
                // }
                
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

