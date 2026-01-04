using Microsoft.UI.Xaml;
using VoiceR.Config;
using VoiceR.Llm;
using VoiceR.Model;

namespace VoiceR
{
    public partial class App : Application
    {
        private MainWindow? _mainWindow;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // dependencies
            ConfigService configService = new ConfigService();
            AutomationService automationService = new AutomationService(configService);
            ISerializer serializer = new YamlSerializer(true);
            IDeserializer deserializer = new JsonSerDe(automationService);
            ILlmService openAIService = new OpenAIService(configService, automationService, serializer, deserializer);

            // main window, currently unused
            _mainWindow = new MainWindow();
            _mainWindow.AppWindow.Hide();

            // tray icon
            TrayIconService trayIconService = new TrayIconService(_mainWindow, automationService, openAIService);
            trayIconService.Initialize();

            // workbench window
            var workbenchWindow = new WorkbenchWindow(automationService, openAIService);
            workbenchWindow.Activate();
        }
    }
}

