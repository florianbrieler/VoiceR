using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace VoiceR
{
    public class TrayIconService : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private readonly Window _mainWindow;
        private bool _disposed = false;

        public TrayIconService(Window mainWindow)
        {
            _mainWindow = mainWindow;
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

            // App icon (centered) - using a label with icon
            ToolStripLabel iconLabel = new ToolStripLabel();
            if (_notifyIcon.Icon != null)
            {
                iconLabel.Image = _notifyIcon.Icon.ToBitmap();
            }
            iconLabel.Text = "VoiceR";
            iconLabel.TextAlign = ContentAlignment.MiddleCenter;
            iconLabel.Enabled = false;
            menu.Items.Add(iconLabel);

            // Separator
            menu.Items.Add(new ToolStripSeparator());

            // Start menu item
            ToolStripMenuItem startItem = new ToolStripMenuItem("Start");
            startItem.Click += (sender, e) =>
            {
                ShowAlert("Start", "Start has been clicked.");
            };
            menu.Items.Add(startItem);

            // Configure menu item
            ToolStripMenuItem configureItem = new ToolStripMenuItem("Configure");
            configureItem.Click += (sender, e) =>
            {
                ShowAlert("Configure", "Configure has been clicked.");
            };
            menu.Items.Add(configureItem);

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

