using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media;
using VoiceR.Llm;
using VoiceR.Model;
using Windows.Foundation;

namespace VoiceR
{
    public sealed partial class AnalyzeWindow : Window
    {
        // Dictionary to map TreeViewNode to UIAutomationTreeNode
        private Dictionary<TreeViewNode, Item> _nodeMap = new Dictionary<TreeViewNode, Item>();
        
        // Reverse mapping from display text to TreeViewNode (since TreeViewItem.Content is the string)
        private Dictionary<string, TreeViewNode> _displayTextToNodeMap = new Dictionary<string, TreeViewNode>();

        // Divider drag state
        private bool _isDraggingDivider = false;
        private Point _dividerDragStartPoint;
        private double _leftColumnStartWidth;

        // Working node tracking (for "Work with this node" feature)
        private TreeViewNode? _workingNode = null;
        private TreeViewItem? _workingTreeViewItem = null;

        public AnalyzeWindow()
        {
            this.InitializeComponent();
            
            // Set window title
            Title = "VoiceR - UI Analysis";
            
            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 800));
            
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
            var itemService = await Task.Run(() => ItemService.Create());

            // Update metrics
            RetrievalTimeText.Text = $"{itemService.RetrievalTimeMs} ms";
            TotalElementsText.Text = itemService.TotalItemCount.ToString("N0");

            // Populate the tree view
            PopulateTreeView(itemService.Root);
        }

        private void PopulateTreeView(Item? rootNode)
        {
            UITreeView.RootNodes.Clear();
            _nodeMap.Clear(); // Clear the mapping when repopulating
            _displayTextToNodeMap.Clear(); // Clear the reverse mapping
            
            var rootTreeNode = CreateTreeViewNode(rootNode);
            UITreeView.RootNodes.Add(rootTreeNode);
        }

        private TreeViewNode CreateTreeViewNode(Item? node)
        {
            if (node == null)
            {
                return new TreeViewNode
                {
                    Content = "(null item)",
                    IsExpanded = false // Initially collapsed
                };
            }

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

            // Check if we found a valid node
            if (targetNode != null && _nodeMap.TryGetValue(targetNode, out var uiNode))
            {
                bool hasExpandCollapse = uiNode.IsPatternAvailable(Pattern.ExpandCollapse);
                bool hasInvoke = uiNode.IsPatternAvailable(Pattern.Invoke);
                bool hasToggle = uiNode.IsPatternAvailable(Pattern.Toggle);
                bool hasTransform = uiNode.IsPatternAvailable(Pattern.Transform);
                bool hasWindow = uiNode.IsPatternAvailable(Pattern.Window);

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

                // Group 4: Arrange (Transform pattern)
                if (hasTransform)
                {
                    if (needsSeparator)
                    {
                        menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    }

                    var arrangeLeftItem = new MenuFlyoutItem { Text = "Arrange Left" };
                    arrangeLeftItem.Click += ArrangeLeftMenuItem_Click;
                    menuFlyout.Items.Add(arrangeLeftItem);

                    var arrangeRightItem = new MenuFlyoutItem { Text = "Arrange Right" };
                    arrangeRightItem.Click += ArrangeRightMenuItem_Click;
                    menuFlyout.Items.Add(arrangeRightItem);

                    var arrangeTopItem = new MenuFlyoutItem { Text = "Arrange Top" };
                    arrangeTopItem.Click += ArrangeTopMenuItem_Click;
                    menuFlyout.Items.Add(arrangeTopItem);

                    var arrangeBottomItem = new MenuFlyoutItem { Text = "Arrange Bottom" };
                    arrangeBottomItem.Click += ArrangeBottomMenuItem_Click;
                    menuFlyout.Items.Add(arrangeBottomItem);

                    needsSeparator = true;
                }

                // Group 5: Window state (Maximize, Minimize, Normal)
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

                    // Group 6: Close Window (separated from window state)
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());

                    var closeItem = new MenuFlyoutItem { Text = "Close Window" };
                    closeItem.Click += CloseWindowMenuItem_Click;
                    menuFlyout.Items.Add(closeItem);

                    needsSeparator = true;
                }

                // Group 7: Work with this node - Always available for any item
                if (needsSeparator)
                {
                    menuFlyout.Items.Add(new MenuFlyoutSeparator());
                }

                var workWithNodeItem = new MenuFlyoutItem { Text = "Work with this item" };
                workWithNodeItem.Click += WorkWithNodeMenuItem_Click;
                menuFlyout.Items.Add(workWithNodeItem);
                
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

            // No valid node found - don't show context menu
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

        private void WorkWithNodeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                // Clear previous highlight
                ClearWorkingNodeHighlight();

                // Serialize the item to JSON and display in Request pane
                string json = ItemJsonSerializer.ToJson(uiNode);
                RequestJsonTextBox.Text = json;

                // Store and highlight the working node
                _workingNode = node;
                HighlightWorkingNode(node);
            }
        }

        private void ClearWorkingNodeHighlight()
        {
            if (_workingTreeViewItem != null)
            {
                // Reset the background to default
                _workingTreeViewItem.Background = null;
                _workingTreeViewItem = null;
            }
            _workingNode = null;
        }

        private void HighlightWorkingNode(TreeViewNode node)
        {
            // Find the TreeViewItem container for this node
            var container = UITreeView.ContainerFromNode(node) as TreeViewItem;
            if (container != null)
            {
                // Apply highlight background
                container.Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue) { Opacity = 0.3 };
                _workingTreeViewItem = container;
            }
        }

        private void ArrangeLeftMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(ItemActions.ArrangeState.Left);
            }
        }

        private void ArrangeRightMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(ItemActions.ArrangeState.Right);
            }
        }

        private void ArrangeTopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(ItemActions.ArrangeState.Top);
            }
        }

        private void ArrangeBottomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(ItemActions.ArrangeState.Bottom);
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

        #region Divider Drag Handling

        private void Divider_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _isDraggingDivider = true;
            _dividerDragStartPoint = e.GetCurrentPoint(this.Content as UIElement).Position;
            _leftColumnStartWidth = LeftColumn.ActualWidth;
            DividerBorder.CapturePointer(e.Pointer);
            e.Handled = true;
        }

        private void Divider_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingDivider) return;

            var currentPoint = e.GetCurrentPoint(this.Content as UIElement).Position;
            var deltaX = currentPoint.X - _dividerDragStartPoint.X;
            var newWidth = _leftColumnStartWidth + deltaX;

            // Clamp to minimum width
            if (newWidth < LeftColumn.MinWidth)
            {
                newWidth = LeftColumn.MinWidth;
            }

            // Update the column width
            LeftColumn.Width = new GridLength(newWidth);
            e.Handled = true;
        }

        private void Divider_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (_isDraggingDivider)
            {
                _isDraggingDivider = false;
                DividerBorder.ReleasePointerCapture(e.Pointer);
                e.Handled = true;
            }
        }

        private void Divider_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            // Change cursor to resize cursor
            if (sender is Border border)
            {
                // Visual feedback - make the divider more visible
                if (border.Child is Border innerBorder)
                {
                    innerBorder.Background = new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
                }
            }
        }

        private void Divider_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (!_isDraggingDivider && sender is Border border)
            {
                // Reset visual feedback
                if (border.Child is Border innerBorder)
                {
                    innerBorder.Background = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                }
            }
        }

        #endregion

        #region Go Button

        private void GoButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement the Go button functionality
            // This will process the prompt with the request JSON and populate the response
            var prompt = PromptTextBox.Text;
            var request = RequestJsonTextBox.Text;
            
            // Placeholder - will be implemented later
            ResponseJsonTextBox.Text = "// Response will appear here after processing";
        }

        #endregion
    }
}

