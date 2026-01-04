using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace VoiceR.Config
{
    public class ConfigService
    {
        private static readonly string ConfigFileName = "appconfig.json";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        public AppConfig Load()
        {
            var config = new AppConfig();

            if (File.Exists(ConfigFilePath))
            {
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                    .AddJsonFile(ConfigFileName, optional: true, reloadOnChange: false)
                    .Build();

                config.OpenAiApiKey = configuration["OpenAiApiKey"] ?? string.Empty;
                config.SystemPrompt = configuration["SystemPrompt"] ?? string.Empty;
                config.MaxDepth = int.TryParse(configuration["MaxDepth"], out int maxDepth) ? maxDepth : 5;
            }

            return config;
        }

        public void Save(AppConfig config)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}

