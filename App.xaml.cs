using System;
using System.IO;
using System.Windows;
using ModernWpf;
using Newtonsoft.Json.Linq;

namespace RobsYTDownloader
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Auto-detect Windows theme
            ThemeManager.Current.ApplicationTheme = null; // null = use system theme

            // Check if OAuth is configured
            if (NeedsFirstTimeSetup())
            {
                // Show first-time setup window
                var setupWindow = new FirstTimeSetupWindow();
                setupWindow.ShowDialog();

                // If user cancelled setup, exit the application
                if (!setupWindow.SetupCompleted)
                {
                    Shutdown();
                    return;
                }
            }

            // Continue with normal startup (MainWindow will be shown automatically via StartupUri in App.xaml)
        }

        private bool NeedsFirstTimeSetup()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oauth_config.json");

                // If file doesn't exist, needs setup
                if (!File.Exists(configPath))
                {
                    return true;
                }

                // Read and check if it's still using placeholder values
                var json = File.ReadAllText(configPath);
                var config = JObject.Parse(json);

                var clientId = config["GoogleOAuth"]?["ClientId"]?.ToString();
                var clientSecret = config["GoogleOAuth"]?["ClientSecret"]?.ToString();

                // Check if using template/placeholder values
                if (string.IsNullOrWhiteSpace(clientId) ||
                    string.IsNullOrWhiteSpace(clientSecret) ||
                    clientId.Contains("YOUR_") ||
                    clientSecret.Contains("YOUR_") ||
                    clientId == "SKIP_SETUP_NOT_CONFIGURED")
                {
                    return true;
                }

                return false;
            }
            catch
            {
                // If there's any error reading the config, show setup
                return true;
            }
        }
    }
}
