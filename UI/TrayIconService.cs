using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using VoiceR.Llm;
using VoiceR.Model;

namespace VoiceR
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private bool _disposed = false;

        // dependencies
        private readonly Window _mainWindow;
        private readonly AutomationService _automationService;
        private readonly ILlmService _openAIService;

        public TrayIconService(Window mainWindow, AutomationService automationService, ILlmService openAIService)
        {
            _mainWindow = mainWindow;
            _automationService = automationService;
            _openAIService = openAIService;
        }

        public void Initialize()
        {
            // Get the icon path
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "VoiceR.png");

            // Create the tray icon

            _notifyIcon = new NotifyIcon();

            // Load icon from PNG file

            if (File.Exists(iconPath))
            {
                using (var stream = new FileStream(iconPath, FileMode.Open, FileAccess.Read))
                {
                    using (var bitmap = new Bitmap(stream))
                    {
                        var iconHandle = bitmap.GetHicon();
                        _notifyIcon.Icon = Icon.FromHandle(iconHandle);
                    }
                }
            }
            else
            {
                // Fallback to default icon if file not found
                _notifyIcon.Icon = SystemIcons.Application;
            }

            _notifyIcon.Text = "VoiceR";
            _notifyIcon.Visible = true;

            // Create context menu
            CreateContextMenu();

            // Handle double-click to show window (optional)
            _notifyIcon.DoubleClick += (sender, e) =>
            {
                // Could show window on double-click if needed
            };
        }

        private void CreateContextMenu()
        {
            if (_notifyIcon == null) return;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Margin = new Padding(36, 36, 36, 36);
            menu.Padding = new Padding(36, 36, 36, 36);
            menu.AutoSize = true;

            // App icon (centered) - using a label with icon
            ToolStripLabel iconLabel = new ToolStripLabel();
            iconLabel.Font = new Font("Segoe UI", 14f, FontStyle.Regular);
            iconLabel.Text = "VoiceR";
            iconLabel.Padding = new Padding(0, 8, 16, 8);
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            iconLabel.Enabled = false;
            menu.Items.Add(iconLabel);

            // // Start menu item
            // ToolStripMenuItem startItem = new ToolStripMenuItem("Start");
            // startItem.Click += (sender, e) =>
            // {
            //     ShowAlert("Start", "Start has been clicked.");
            // };
            // menu.Items.Add(startItem);

            // Configure menu item
            ToolStripMenuItem configureItem = new ToolStripMenuItem("Configure");
            configureItem.Click += (sender, e) =>
            {
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    var configWindow = new ConfigWindow();
                    configWindow.Activate();
                });
            };
            menu.Items.Add(configureItem);

            // Workbench menu item
            ToolStripMenuItem workbenchItem = new ToolStripMenuItem("Workbench");
            workbenchItem.Click += (sender, e) =>
            {
                // Create and show the analyze window on the UI thread
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    var workbenchWindow = new WorkbenchWindow(_automationService, _openAIService);
                    workbenchWindow.Activate();
                });
            };
            menu.Items.Add(workbenchItem);

            // Separator
            menu.Items.Add(new ToolStripSeparator());

            // Quit menu item
            ToolStripMenuItem quitItem = new ToolStripMenuItem("Quit");
            quitItem.Click += (sender, e) =>
            {
                Microsoft.UI.Xaml.Application.Current.Exit();
            };
            menu.Items.Add(quitItem);

            _notifyIcon.ContextMenuStrip = menu;
        }

        private void ShowAlert(string title, string message)
        {
            // Show alert using Windows Forms MessageBox
            // This works reliably from the tray icon context menu
            MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
                _disposed = true;
            }
        }
    }
}

