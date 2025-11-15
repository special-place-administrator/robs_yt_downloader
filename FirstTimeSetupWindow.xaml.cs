using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Newtonsoft.Json.Linq;

namespace RobsYTDownloader
{
    public partial class FirstTimeSetupWindow : Window
    {
        private string _clientSecret = "";
        public bool SetupCompleted { get; private set; } = false;

        public FirstTimeSetupWindow()
        {
            InitializeComponent();
            Topmost = true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

        private void ClientSecretPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            _clientSecret = ClientSecretPasswordBox.Password;
        }

        private bool ValidateInputs()
        {
            bool isValid = true;

            // Validate Client ID
            if (string.IsNullOrWhiteSpace(ClientIdTextBox.Text))
            {
                ClientIdError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                ClientIdError.Visibility = Visibility.Collapsed;
            }

            // Validate Client Secret
            if (string.IsNullOrWhiteSpace(_clientSecret))
            {
                ClientSecretError.Visibility = Visibility.Visible;
                isValid = false;
            }
            else
            {
                ClientSecretError.Visibility = Visibility.Collapsed;
            }

            return isValid;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs())
            {
                MessageBox.Show(
                    "Please fill in all required fields (Client ID and Client Secret).",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create OAuth configuration
                var config = new JObject
                {
                    ["GoogleOAuth"] = new JObject
                    {
                        ["ClientId"] = ClientIdTextBox.Text.Trim(),
                        ["ClientSecret"] = _clientSecret.Trim(),
                        ["RedirectUri"] = RedirectUriTextBox.Text.Trim(),
                        ["Scope"] = ScopeTextBox.Text.Trim()
                    }
                };

                // Save to oauth_config.json
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oauth_config.json");
                File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                SetupCompleted = true;

                MessageBox.Show(
                    "OAuth configuration saved successfully!\n\nYou can now use the Google Login feature to access high-quality video downloads.",
                    "Setup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving configuration:\n\n{ex.Message}",
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to skip the OAuth setup?\n\n" +
                "Without OAuth configuration, you will not be able to:\n" +
                "• Download high-quality videos (4K/8K)\n" +
                "• Access members-only content\n" +
                "• Download age-restricted videos\n\n" +
                "You can always configure OAuth later in Settings.",
                "Skip OAuth Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Create a minimal config file with template values
                try
                {
                    var config = new JObject
                    {
                        ["GoogleOAuth"] = new JObject
                        {
                            ["ClientId"] = "SKIP_SETUP_NOT_CONFIGURED",
                            ["ClientSecret"] = "SKIP_SETUP_NOT_CONFIGURED",
                            ["RedirectUri"] = "http://localhost:8080/",
                            ["Scope"] = "https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/userinfo.email"
                        }
                    };

                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "oauth_config.json");
                    File.WriteAllText(configPath, config.ToString(Newtonsoft.Json.Formatting.Indented));

                    SetupCompleted = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error creating configuration file:\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to exit?\n\nThe application will close without saving any configuration.",
                "Exit Setup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                SetupCompleted = false;
                Application.Current.Shutdown();
            }
        }
    }
}
