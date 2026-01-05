using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using VoiceR.Config;
using VoiceR.Llm;
using VoiceR.Model;
using VoiceR.UI;
using VoiceR.Voice;
using Whisper.net.Ggml;
using Windows.Foundation;
using Windows.UI;

namespace VoiceR
{
    public sealed partial class WorkbenchWindow : Window
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
        private long _lastRetrievalTimeMs = -1;

        // dependencies
        private readonly AutomationService _automationService;
        private readonly ILlmService _llmService;

        private LlmResult? _lastLlmResult = null;

        // Voice recording
        private WhisperConverter? _whisperConverter;
        private bool _hasVoiceInitialized = false;

        public WorkbenchWindow(AutomationService automationService, ILlmService llmService)
        {
            this.InitializeComponent();

            // set dependencies
            _automationService = automationService;
            _llmService = llmService;

            // Set window title
            Title = "VoiceR - Workbench";

            // Set window icon

            IconHelper.SetWindowIcon(this);

            // Set window size

            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 800));

            // Populate model combo box

            foreach (var model in _llmService.AvailableModels)
            {
                ModelComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{model.Name} (${model.InputPricePerMillion} / ${model.OutputPricePerMillion})",
                    Tag = model
                });
            }
            ModelComboBox.SelectedIndex = 0;

            // Populate Whisper model combo box
            PopulateWhisperModelComboBox();

            // Load UI tree when window is activated
            this.Activated += WorkbenchWindow_Activated;
            this.Closed += WorkbenchWindow_Closed;
        }

        private void PopulateWhisperModelComboBox()
        {
            WhisperModelComboBox.Items.Clear();

            var models = new[]
            {
                (GgmlType.Tiny, "Tiny (~75MB)"),
                (GgmlType.Base, "Base (~142MB)"),
                (GgmlType.Small, "Small (~466MB)"),
                (GgmlType.Medium, "Medium (~1.5GB)"),
                (GgmlType.LargeV2, "Large V2 (~2.9GB)")
            };

            foreach (var (modelType, displayName) in models)
            {
                WhisperModelComboBox.Items.Add(new ComboBoxItem
                {
                    Content = displayName,
                    Tag = modelType
                });
            }

            // Default to Base model
            WhisperModelComboBox.SelectedIndex = 1;
        }

        private bool _hasLoaded = false;

        private void WorkbenchWindow_Activated(object sender, WindowActivatedEventArgs args) // was: async
        {
            // Only load once
            if (_hasLoaded) return;
            _hasLoaded = true;

            UIElementsDetails.Text = "loading...";

            Stopwatch stopwatch = Stopwatch.StartNew();
            _automationService.PerformScan();
            stopwatch.Stop();
            _lastRetrievalTimeMs = stopwatch.ElapsedMilliseconds;

            // Update metrics
            UIElementsDetails.Text =
                $"Retrieval time: {_lastRetrievalTimeMs}ms\n" +
                $"Total elements: {_automationService.Root?.Size().ToString("N0") ?? "(error occurred)"}";

            // Populate the tree view with full tree
            PopulateTreeView(_automationService.Root);

            // Initialize voice recording
            if (!_hasVoiceInitialized)
            {
                _hasVoiceInitialized = true;
                _ = InitializeVoiceAsync();
            }
        }

        private async Task InitializeVoiceAsync()
        {
            try
            {
                _whisperConverter = new WhisperConverter();
                _whisperConverter.RecordingStateChanged += WhisperConverter_RecordingStateChanged;

                // Initialize with default model (Base)
                var defaultModel = WhisperModelComboBox.SelectedItem as ComboBoxItem;
                if (defaultModel?.Tag is GgmlType modelType)
                {
                    await _whisperConverter.InitializeAsync(modelType);
                }
            }
            catch (Exception ex)
            {
                // Show error but don't block the UI
                System.Diagnostics.Debug.WriteLine($"Failed to initialize voice recording: {ex.Message}");
            }
        }

        private void WhisperConverter_RecordingStateChanged(object? sender, bool isRecording)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateVoiceStatusIndicator(isRecording);
            });
        }

        private void UpdateVoiceStatusIndicator(bool isRecording)
        {
            if (isRecording)
            {
                // Red when recording
                VoiceStatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 244, 67, 54)); // #F44336
            }
            else
            {
                // Green when ready
                VoiceStatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 175, 80)); // #4CAF50
            }
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

            // Create styled TextBlock based on LoI
            var textBlock = new TextBlock { Text = node.DisplayText };

            if (node.LoI == Item.LevelOfInformation.None)
            {
                textBlock.TextDecorations = Windows.UI.Text.TextDecorations.Strikethrough;
            }
            else if (node.LoI == Item.LevelOfInformation.Connector)
            {
                textBlock.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGray);
            }

            var treeViewNode = new TreeViewNode
            {
                Content = textBlock,
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
                        // If Content is a TextBlock, extract the text and use the reverse mapping
                        else if (treeViewItem.Content is TextBlock textBlock)
                        {
                            var displayText = textBlock.Text;
                            targetNode = FindNodeByDisplayText(displayText);
                            if (targetNode != null) break;
                        }
                        // If Content is a string (display text), use the reverse mapping
                        else if (treeViewItem.Content is string displayText)
                        {
                            targetNode = FindNodeByDisplayText(displayText);
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
                bool hasFullLoI = uiNode.LoI == Item.LevelOfInformation.Full || uiNode == _automationService.CompactRoot || uiNode == _automationService.Root;

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

                // Group 7: Work with this node
                if (hasFullLoI)
                {
                    if (needsSeparator)
                    {
                        menuFlyout.Items.Add(new MenuFlyoutSeparator());
                    }

                    var workWithNodeItem = new MenuFlyoutItem { Text = "Work with this item" };
                    workWithNodeItem.Click += WorkWithNodeMenuItem_Click;
                    menuFlyout.Items.Add(workWithNodeItem);
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
            if (node != null && _nodeMap.TryGetValue(node, out Item? uiNode) && uiNode != null)
            {
                // Clear previous highlight
                ClearWorkingNodeHighlight();

                // Serialize the item and display in Request pane
                _llmService.Scope = uiNode;
                RequestJsonTextBox.Text = _llmService.SerializedContext;

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
                uiNode.Arrange(Actions.ArrangeState.Left);
            }
        }

        private void ArrangeRightMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(Actions.ArrangeState.Right);
            }
        }

        private void ArrangeTopMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(Actions.ArrangeState.Top);
            }
        }

        private void ArrangeBottomMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var node = GetContextMenuTargetNode();
            if (node != null && _nodeMap.TryGetValue(node, out var uiNode))
            {
                uiNode.Arrange(Actions.ArrangeState.Bottom);
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

        private TreeViewNode? FindNodeByDisplayText(string displayText)
        {
            // Try exact match first
            if (_displayTextToNodeMap.TryGetValue(displayText, out var mappedNode))
            {
                return mappedNode;
            }

            // If exact match fails, try to find by matching the base display text
            // (handles the case where we added a suffix for uniqueness)

            foreach (var kvp in _displayTextToNodeMap)
            {
                var baseKey = kvp.Key.Contains('_') ? kvp.Key.Substring(0, kvp.Key.LastIndexOf('_')) : kvp.Key;
                if (baseKey == displayText || displayText == kvp.Key)
                {
                    return kvp.Value;
                }
            }


            return null;
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

        #region Tree View Toggle

        private void TreeViewToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_automationService == null) return;

            bool isCompactMode = TreeViewToggle.IsChecked == true;
            Item? item = isCompactMode ? _automationService.CompactRoot : _automationService.Root;
            string label = isCompactMode ? "Compact tree" : "Full tree";

            TreeViewToggle.Content = label;
            PopulateTreeView(item);
            UIElementsDetails.Text =
                $"Retreival time: {_lastRetrievalTimeMs} ms\n" +
                $"Total elements: {(item?.Size().ToString("N0") ?? "-")}";
        }

        #endregion

        #region Go Button

        private async void GoButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteButton.IsEnabled = false;

            // get prompt
            string prompt = PromptTextBox.Text;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                ResponseDetails.Text = "Error: Please enter a prompt.";
                return;
            }

            // get selected model
            if (ModelComboBox.SelectedItem is not ComboBoxItem { Tag: LargeLanguageModel selectedModel })
            {
                ResponseDetails.Text = "Error: Please select a model.";
                return;
            }

            // show loading state
            GoButton.IsEnabled = false;
            ModelComboBox.IsEnabled = false;
            ResponseDetails.Text = "Generating response...";
            ResponseTextBox.Text = "";

            // generate response
            try
            {
                _llmService.Model = selectedModel;

                _llmService.Scope = _workingNode != null ? _nodeMap[_workingNode] : null;
                LlmResult result = await _llmService.GenerateAsync(prompt);


                ResponseDetails.Text =
                    $"Input tokens: {result.InputTokens} (est. ${result.EstimatedInputPriceUSD})\n" +
                    $"Output tokens: ${result.OutputTokens} (est. ${result.EstimatedOutputPriceUSD})\n" +
                    $"Duration: {result.ElapsedMilliseconds}ms" +
                    (result.Errors.Count > 0 ? "\n" + string.Join("\n", result.Errors) : $"\nno errors, extracted {result.Actions.Count} action(s)");
                ResponseTextBox.Text = result.Response;
                ExecuteButton.IsEnabled = result.Errors.Count == 0;

                _lastLlmResult = result;

                // auto execute if no errors and auto execute checkbox is checked
                if (result.Errors.Count == 0 && AutoExecuteCheckBox.IsChecked == true)
                {
                    ExecuteButton_Click(ExecuteButton, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                ResponseDetails.Text = $"Error: {ex.Message}";
            }
            finally
            {
                GoButton.IsEnabled = true;
                ModelComboBox.IsEnabled = true;
            }
        }

        #endregion

        #region Execute Button

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastLlmResult == null)
            {
                return;
            }

            foreach (Action action in _lastLlmResult.Actions)
            {
                action();
            }
        }

        #endregion

        #region Prompt TextBox

        private void PromptTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Check for Ctrl+Enter
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var controlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                bool isCtrlDown = (controlPressed & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;

                if (isCtrlDown)
                {
                    // Invoke the Go button
                    GoButton_Click(GoButton, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Voice Recording

        private async void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            if (_whisperConverter == null)
            {
                return;
            }

            try
            {
                if (!_whisperConverter.IsRecording)
                {
                    // Start recording
                    _whisperConverter.StartRecording();
                    ListenButton.Content = "Stop";
                    ReplayButton.IsEnabled = false;
                    AudioDetails.Text = "-";
                }
                else
                {
                    // Stop recording and transcribe
                    ListenButton.IsEnabled = false;

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    string transcribedText = await _whisperConverter.StopRecordingAsync();
                    stopwatch.Stop();
                    long transcriptionTimeMs = stopwatch.ElapsedMilliseconds;
                    AudioDetails.Text = $"Transcription time: {transcriptionTimeMs}ms";

                    // Append transcribed text to prompt box
                    if (!string.IsNullOrWhiteSpace(transcribedText))
                    {
                        if (AutoProcessCheckBox.IsChecked == false)
                        {
                            string currentText = PromptTextBox.Text;
                            bool needsSeparator = !string.IsNullOrWhiteSpace(currentText) && !currentText.EndsWith(" ") && !currentText.EndsWith("\n");
                            PromptTextBox.Text = currentText + (needsSeparator ? "\n" : "") + transcribedText;
                        }
                        else
                        {
                            PromptTextBox.Text = transcribedText;
                            GoButton_Click(GoButton, new RoutedEventArgs());
                        }
                    }

                    ListenButton.Content = "Listen";
                    ListenButton.IsEnabled = true;
                    ReplayButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ListenButton.Content = "Listen";
                ListenButton.IsEnabled = true;
                System.Diagnostics.Debug.WriteLine($"Voice recording error: {ex.Message}");
                // Could show a message to the user here
            }
        }

        private async void ReplayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_whisperConverter == null)
            {
                return;
            }

            try
            {
                ReplayButton.IsEnabled = false;
                await _whisperConverter.ReplayLastRecordingAsync();
                ReplayButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                ReplayButton.IsEnabled = true;
                System.Diagnostics.Debug.WriteLine($"Replay error: {ex.Message}");
                // Could show a message to the user here
            }
        }

        private async void WhisperModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_whisperConverter == null || WhisperModelComboBox.SelectedItem is not ComboBoxItem selectedItem)
            {
                return;
            }

            if (selectedItem.Tag is GgmlType modelType)
            {
                try
                {
                    // Disable controls while switching models
                    WhisperModelComboBox.IsEnabled = false;
                    ListenButton.IsEnabled = false;

                    await _whisperConverter.InitializeAsync(modelType);

                    // Re-enable controls
                    WhisperModelComboBox.IsEnabled = true;
                    ListenButton.IsEnabled = true;
                }
                catch (Exception ex)
                {
                    WhisperModelComboBox.IsEnabled = true;
                    ListenButton.IsEnabled = true;
                    System.Diagnostics.Debug.WriteLine($"Failed to switch Whisper model: {ex.Message}");
                    // Could show a message to the user here
                }
            }
        }

        private void WorkbenchWindow_Closed(object sender, WindowEventArgs args)
        {
            _whisperConverter?.Dispose();
            _whisperConverter = null;
        }

        #endregion
    }
}

