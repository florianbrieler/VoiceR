using Microsoft.UI.Xaml;
using VoiceR.Config;
using VoiceR.UI;

namespace VoiceR
{
    public sealed partial class ConfigWindow : Window
    {
        private readonly ConfigService _configService;

        public ConfigWindow()
        {
            this.InitializeComponent();

            Title = "VoiceR - Configuration";

            // Set window icon
            IconHelper.SetWindowIcon(this);

            // Set window size
            var appWindow = this.AppWindow;
            appWindow.Resize(new Windows.Graphics.SizeInt32(600, 500));

            _configService = new ConfigService();
            LoadConfig();
        }

        private void LoadConfig()
        {
            var config = _configService.Load();
            ApiKeyTextBox.Text = config.OpenAiApiKey;
            SystemPromptTextBox.Text = config.SystemPrompt;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var config = new AppConfig
            {
                OpenAiApiKey = ApiKeyTextBox.Text,
                SystemPrompt = SystemPromptTextBox.Text
            };

            _configService.Save(config);
            this.Close();
        }
    }
}

