using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;

namespace VoiceR
{
    public sealed partial class AnalyzeWindow : Window
    {
        // Dictionary to map TreeViewNode to UIAutomationTreeNode
        private Dictionary<TreeViewNode, Item> _nodeMap = new Dictionary<TreeViewNode, Item>();
        
        // Reverse mapping from display text to TreeViewNode (since TreeViewItem.Content is the string)
        private Dictionary<string, TreeViewNode> _displayTextToNodeMap = new Dictionary<string, TreeViewNode>();

        public AnalyzeWindow()
        {
            this.InitializeComponent();
            
            // Set window title
            Title = "VoiceR - UI Analysis";
            
            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));
            
            // Load UI tree when window is activated
            this.Activated += AnalyzeWindow_Activated;
        }

        private bool _hasLoaded = false;

        private async void AnalyzeWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            // Only load once
            if (_hasLoaded) return;
            _hasLoaded = true;

            // Show loading state
            RetrievalTimeText.Text = "Scanning...";
            TotalElementsText.Text = "...";

            // Run the scan on a background thread to keep UI responsive
            var result = await Task.Run(() => ItemService.ScanUITree());

            // Update metrics
            RetrievalTimeText.Text = $"{result.RetrievalTimeMs} ms";
            TotalElementsText.Text = result.TotalNodeCount.ToString("N0");

            // Populate the tree view
            PopulateTreeView(result.RootNode);
        }

        private void PopulateTreeView(Item rootNode)
        {
            UITreeView.RootNodes.Clear();
            _nodeMap.Clear(); // Clear the mapping when repopulating
            _displayTextToNodeMap.Clear(); // Clear the reverse mapping
            
            var rootTreeNode = CreateTreeViewNode(rootNode);
            UITreeView.RootNodes.Add(rootTreeNode);
        }

        private TreeViewNode CreateTreeViewNode(Item node)
        {
            var treeViewNode = new TreeViewNode
            {
                Content = node.DisplayText,
                IsExpanded = false // Initially collapsed
            };

            // Store the mapping
            _nodeMap[treeViewNode] = node;
            
            // Store reverse mapping (use a unique key in case of duplicate display texts)
            var displayText = node.DisplayText;
            var uniqueKey = displayText;
            int counter = 0;
            while (_displayTextToNodeMap.ContainsKey(uniqueKey))
            {
                uniqueKey = $"{displayText}_{counter}";
                counter++;
            }
            _displayTextToNodeMap[uniqueKey] = treeViewNode;

            foreach (var child in node.GetChildren())
            {
                var childTreeNode = CreateTreeViewNode(child);
                treeViewNode.Children.Add(childTreeNode);
            }

            return treeViewNode;
        }

        private void TreeView_ContextRequested(UIElement sender, Microsoft.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            // Find the TreeViewNode that was right-clicked
            TreeViewNode? targetNode = null;
            
            if (args.OriginalSource is DependencyObject source)
            {
                // Walk up the visual tree to find the TreeViewItem container
                var current = source;
                while (current != null)
                {
                    // In WinUI 3, TreeViewItem's Content is the display text (string), not the TreeViewNode
                    if (current is TreeViewItem treeViewItem)
                    {
                        // Try to get the TreeViewNode from the Content (if it's a TreeViewNode)
                        if (treeViewItem.Content is TreeViewNode node)
                        {
                            targetNode = node;
                            break;
                        }
                        // If Content is a string (display text), use the reverse mapping
                        else if (treeViewItem.Content is string displayText)
                        {
                            // Try exact match first
                            if (_displayTextToNodeMap.TryGetValue(displayText, out var mappedNode))
                            {
                                targetNode = mappedNode;
                                break;
                            }
                            
                            // If exact match fails, try to find by matching the base display text
                            // (handles the case where we added a suffix for uniqueness)
                            foreach (var kvp in _displayTextToNodeMap)
                            {
                                var baseKey = kvp.Key.Contains('_') ? kvp.Key.Substring(0, kvp.Key.LastIndexOf('_')) : kvp.Key;
                                if (baseKey == displayText || displayText == kvp.Key)
                                {
                                    targetNode = kvp.Value;
                                    break;
                                }
                            }
                            
                            if (targetNode != null) break;
                        }
                        // Alternative: try to get from DataContext
                        else if (treeViewItem.DataContext is TreeViewNode dataNode)
                        {
                            targetNode = dataNode;
                            break;
                        }
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            // Check if we found a valid node with any actionable patterns
            if (targetNode != null && _nodeMap.TryGetValue(targetNode, out var uiNode))
            {
                bool hasExpandCollapse = uiNode.IsPatternAvailable(Pattern.ExpandCollapse);
                bool hasInvoke = uiNode.IsPatternAvailable(Pattern.Invoke);
                bool hasToggle = uiNode.IsPatternAvailable(Pattern.Toggle);
                bool hasWindow = uiNode.IsPatternAvailable(Pattern.Window);

                // Only show context menu if at least one actionable pattern is available
                if (hasExpandCollapse || hasInvoke || hasToggle || hasWindow)
                {
                    // Store the target node for menu item handlers
                    _contextMenuTargetNode = targetNode;
                    
                    // Create and show the context menu programmatically
                    var menuFlyout = new MenuFlyout();
                    bool needsSeparator = false;

                    // Group 1: Expand/Collapse
                    if (hasExpandCollapse)
                    {
                        var expandItem = new MenuFlyoutItem { Text = "Expand" };
                        expandItem.Click += ExpandMenuItem_Click;
                        menuFlyout.Items.Add(expandItem);

                        var collapseItem = new MenuFlyoutItem { Text = "Collapse" };
                        collapseItem.Click += CollapseMenuItem_Click;
                        menuFlyout.Items.Add(collapseItem);

                        needsSeparator = true;
                    }

                    // Group 2: Invoke
                    if (hasInvoke)
                    {
                        if (needsSeparator)
                        {
                            menuFlyout.Items.Add(new MenuFlyoutSeparator());
                        }

                        var invokeItem = new MenuFlyoutItem { Text = "Invoke" };
                        invokeItem.Click += InvokeMenuItem_Click;
                        menuFlyout.Items.Add(invokeItem);

                        needsSeparator = true;
                    }

                    // Group 3: Toggle
                    if (hasToggle)
                    {
                        if (needsSeparator)
                        {
                            menuFlyout.Items.Add(new MenuFlyoutSeparator());
                        }

                        var toggleItem = new MenuFlyoutItem { Text = "Toggle" };
                        toggleItem.Click += ToggleMenuItem_Click;
                        menuFlyout.Items.Add(toggleItem);

                        needsSeparator = true;
                    }

                    // Group 4: Window state (Maximize, Minimize, Normal)
                    if (hasWindow)
                    {
                        if (needsSeparator)
                        {
                            menuFlyout.Items.Add(new MenuFlyoutSeparator());
                        }

                        var maximizeItem = new MenuFlyoutItem { Text = "Maximize" };
                        maximizeItem.Click += MaximizeMenuItem_Click;
                        menuFlyout.Items.Add(maximizeItem);
                        
                        var minimizeItem = new MenuFlyoutItem { Text = "Minimize" };
                        minimizeItem.Click += MinimizeMenuItem_Click;
                        menuFlyout.Items.Add(minimizeItem);
                        
                        var normalItem = new MenuFlyoutItem { Text = "Normal" };
                        normalItem.Click += NormalMenuItem_Click;
                        menuFlyout.Items.Add(normalItem);

                        // Group 5: Close Window (separated from window state)
                        menuFlyout.Items.Add(new MenuFlyoutSeparator());

                        var closeItem = new MenuFlyoutItem { Text = "Close Window" };
                        closeItem.Click += CloseWindowMenuItem_Click;
                        menuFlyout.Items.Add(closeItem);
                    }
                    
                    // Show the menu at the pointer position
                    if (sender is FrameworkElement frameworkElement)
                    {
                        if (args.TryGetPosition(sender, out var point))
                        {
                            menuFlyout.ShowAt(frameworkElement, point);
                        }
                        else
                        {
                            menuFlyout.ShowAt(frameworkElement);
                        }
                    }
                    
                    args.Handled = true;
                    return;
                }
            }

            // No actionable patterns available - don't show context menu
            _contextMenuTargetNode = null;
            args.Handled = true;
        }

        private TreeViewNode? _contextMenuTargetNode = null;

        private void ExpandMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.ExpandOrCollapse(ExpandCollapseState.Expanded);
            }
        }

        private void CollapseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.ExpandOrCollapse(ExpandCollapseState.Collapsed);
            }
        }

        private void InvokeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Invoke();
            }
        }

        private void ToggleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Toggle();
            }
        }

        private void MaximizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.SetWindowVisualState(WindowVisualState.Maximized);
            }
        }

        private void MinimizeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.SetWindowVisualState(WindowVisualState.Minimized);
            }
        }

        private void NormalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.SetWindowVisualState(WindowVisualState.Normal);
            }
        }

        private void CloseWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.CloseWindow();
            }
        }

        private TreeViewNode? GetContextMenuTargetNode()
        {
            // Use the node that was right-clicked (stored when context menu was requested)
            if (_contextMenuTargetNode != null)
            {
                return _contextMenuTargetNode;
            }

            // Fallback to selected node if available
            return UITreeView.SelectedNode;
        }
    }
}

