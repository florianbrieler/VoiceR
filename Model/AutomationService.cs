using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;
using VoiceR.Config;

namespace VoiceR.Model
{
    /// <summary>
    /// Service for retrieving UI elements using Microsoft UI Automation.
    /// </summary>
    public class AutomationService
    {
        private readonly Dictionary<string, Item> _itemById = new();

        // dependencies
        private readonly ConfigService _configService;

        /// <summary>
        /// The root item of the UI tree.
        /// </summary>
        public Item? Root { get; private set; }

        public Item? CompactRoot { get; private set; }

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

        public AutomationService(ConfigService configService)
        {
            _configService = configService;
        }

        /// <summary>
        /// Scans the entire Windows UI tree and populates the service properties.
        /// </summary>
        public void PerformScan()
        {
            try
            {
                // Get the desktop root element
                AutomationElement rootElement = AutomationElement.RootElement;

                // Build the tree recursively
                int maxDepth = _configService.Load().MaxDepth;
                Root = CollectAllItems(rootElement, _itemById, maxDepth);
                Root.CheckLevelOfInformation();
                UpdateCompactRoot();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning UI tree: {ex.Message}");

                Root = null;
                _itemById.Clear();
            }
        }

        /// <summary>
        /// Recursively builds a tree from an AutomationElement.
        /// </summary>
        private static Item CollectAllItems(AutomationElement element, Dictionary<string, Item> itemById, int maxDepth, int depth = 0)
        {
            Item item = Item.FromElement(element);
            itemById[item.Id] = item;

            if (depth >= maxDepth)
            {
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
                        Item childItem = CollectAllItems(child, itemById, maxDepth, depth + 1);
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
            if (Root == null)
            {
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

