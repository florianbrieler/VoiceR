using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace VoiceR
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;
        private TrayIconService? _trayIconService;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _mainWindow = new MainWindow();
            
            // Hide the window immediately before it can be shown
            _mainWindow.AppWindow.Hide();

            // Initialize tray icon service
            _trayIconService = new TrayIconService(_mainWindow);
            _trayIconService.Initialize();
        }

    }
}

