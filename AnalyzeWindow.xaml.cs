using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;

namespace VoiceR
{
    public sealed partial class AnalyzeWindow : Window
    {
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
            var result = await Task.Run(() => UIAutomationService.ScanUITree());

            // Update metrics
            RetrievalTimeText.Text = $"{result.RetrievalTimeMs} ms";
            TotalElementsText.Text = result.TotalNodeCount.ToString("N0");

            // Populate the tree view
            PopulateTreeView(result.RootNode);
        }

        private void PopulateTreeView(UIAutomationTreeNode rootNode)
        {
            UITreeView.RootNodes.Clear();
            
            var rootTreeNode = CreateTreeViewNode(rootNode);
            UITreeView.RootNodes.Add(rootTreeNode);
        }

        private TreeViewNode CreateTreeViewNode(UIAutomationTreeNode node)
        {
            var treeViewNode = new TreeViewNode
            {
                Content = node.DisplayText,
                IsExpanded = false // Initially collapsed
            };

            foreach (var child in node.Children)
            {
                var childTreeNode = CreateTreeViewNode(child);
                treeViewNode.Children.Add(childTreeNode);
            }

            return treeViewNode;
        }
    }
}

