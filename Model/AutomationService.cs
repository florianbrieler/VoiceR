using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;

namespace VoiceR.Model
{
    /// <summary>
    /// Service for retrieving UI elements using Microsoft UI Automation.
    /// </summary>
    public class AutomationService
    {

        public const int MaxDepth = 5;

        private readonly Dictionary<string, Item> _itemById = new();

        /// <summary>
        /// The root item of the UI tree.
        /// </summary>
        public Item? Root { get; private set; }

        public Item? CompactRoot { get; private set; }

        /// <summary>
        /// Time taken to retrieve all UI elements in milliseconds.
        /// </summary>
        public long RetrievalTimeMs { get; private set; }

        /// <summary>
        /// Total count of all item (inner items + leaves).
        /// </summary>
        public int TotalItemCount { get; private set; }

        /// <summary>
        /// Gets an item by its unique ID.
        /// </summary>
        /// <param name="id">The unique ID of the item.</param>
        /// <returns>The Item with the specified ID.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when no item with the specified ID exists.</exception>
        public Item GetItemForId(string id)
        {
            if (_itemById.TryGetValue(id, out var item))
            {
                return item;
            }
            throw new KeyNotFoundException($"No item found with ID: {id}");
        }

        /// <summary>
        /// Creates a new ItemService instance by scanning the entire Windows UI tree.
        /// </summary>
        /// <returns>A new ItemService instance with scan results.</returns>
        public static AutomationService Create()
        {
            var service = new AutomationService();
            service.PerformScan();
            return service;
        }

        /// <summary>
        /// Scans the entire Windows UI tree and populates the service properties.
        /// </summary>
        private void PerformScan()
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Get the desktop root element
                AutomationElement rootElement = AutomationElement.RootElement;

                // Build the tree recursively
                int itemCount = 0;
                Root = CollectAllItems(rootElement, _itemById, ref itemCount);
                TotalItemCount = itemCount;
                Root.CheckLevelOfInformation();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning UI tree: {ex.Message}");

                Root = null;
                TotalItemCount = 1;
                _itemById.Clear();
            }

            stopwatch.Stop();
            RetrievalTimeMs = stopwatch.ElapsedMilliseconds;
        }

        /// <summary>
        /// Recursively builds a tree from an AutomationElement.
        /// </summary>
        private static Item CollectAllItems(AutomationElement element, Dictionary<string, Item> itemById, ref int itemCount, int depth=0)
        {
            itemCount++;

            Item item = Item.FromElement(element);
            itemById[item.Id] = item;

            if (depth >= MaxDepth) {
                return item;
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
                        Item childItem = CollectAllItems(child, itemById, ref itemCount, depth + 1);
                        item.AddChild(childItem);
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

            return item;
        }

        public void UpdateCompactRoot()
        {
            if (Root == null) {
                throw new InvalidOperationException("Root is null");
            }

            CompactRoot = Item.FromItem(Root);

            foreach (Item child in Root.GetChildren())
            {
                if (child.LoI == Item.LevelOfInformation.None) 
                {
                    continue;
                }
                CompactRoot.AddChildren(CreateCompactCopyChildren(child));
            }
        }

        private List<Item> CreateCompactCopyChildren(Item item)
        {
            List<Item> compactCopyChildren = [];
            foreach (Item child in item.GetChildren())
            {
                if (child.LoI == Item.LevelOfInformation.None) 
                {
                    continue;
                }
                compactCopyChildren.AddRange(CreateCompactCopyChildren(child));
            }
            if (item.LoI != Item.LevelOfInformation.Full)
            {
                return compactCopyChildren;
            }
            Item copy = Item.FromItem(item);
            copy.AddChildren(compactCopyChildren);
            return [copy];
        }
    }
}

