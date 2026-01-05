using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Serilog;
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
            Microsoft.Extensions.Logging.ILogger logger = CreateLogger();
            logger.LogInformation("Starting VoiceR");

            ConfigService configService = new ConfigService();
            AutomationService automationService = new AutomationService(configService, logger);
            ISerializer serializer = new YamlSerializer(true);
            IDeserializer deserializer = new JsonSerDe(automationService);
            ILlmService openAIService = new OpenAIService(configService, automationService, serializer, deserializer, logger);

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

        private Microsoft.Extensions.Logging.ILogger CreateLogger()
        {
            // 1. Create your Serilog configuration
            Serilog.Core.Logger serilogLogger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("logs/voicer-.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // 2. Create a LoggerFactory and tell it to use Serilog
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(serilogLogger);
            });

            // 3. Create the ILogger for a specific class
            return loggerFactory.CreateLogger<App>();
        }
    }
}

