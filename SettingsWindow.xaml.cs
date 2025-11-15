using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RobsYTDownloader.Models;
using RobsYTDownloader.Services;

namespace RobsYTDownloader
{
    public partial class SettingsWindow : Window
    {
        private readonly DependencyManager _dependencyManager;
        private readonly GoogleAuthService _authService;
        private readonly ConfigManager _configManager;
        private List<DependencyStatus> _dependencies = new();

        public SettingsWindow()
        {
            InitializeComponent();
            _dependencyManager = new DependencyManager();
            _authService = GoogleAuthService.Instance;
            _configManager = new ConfigManager();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            await CheckDependencies();
            UpdateAuthUI();
        }

        private void LoadSettings()
        {
            var config = _configManager.LoadConfig();
            DownloadFolderTextBox.Text = config.DownloadFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";

            var connections = config.MaxConnections > 0 ? config.MaxConnections : 16;
            var index = connections switch
            {
                4 => 0,
                8 => 1,
                12 => 2,
                16 => 3,
                20 => 4,
                _ => 3
            };
            ConnectionsComboBox.SelectedIndex = index;
        }

        private async System.Threading.Tasks.Task CheckDependencies()
        {
            CheckAllButton.IsEnabled = false;

            _dependencies = new List<DependencyStatus>
            {
                await _dependencyManager.CheckDependency("yt-dlp"),
                await _dependencyManager.CheckDependency("aria2c"),
                await _dependencyManager.CheckDependency("ffmpeg"),
                await _dependencyManager.CheckDependency("node")
            };

            DependenciesListView.ItemsSource = _dependencies;
            CheckAllButton.IsEnabled = true;
        }

        private async void CheckAllButton_Click(object sender, RoutedEventArgs e)
        {
            await CheckDependencies();
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string toolName)
            {
                button.IsEnabled = false;
                try
                {
                    var result = await _dependencyManager.InstallDependency(toolName);
                    if (result)
                    {
                        MessageBox.Show($"{toolName} installed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                        await CheckDependencies();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to install {toolName}. Please install manually.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error installing {toolName}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    button.IsEnabled = true;
                }
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginButton.IsEnabled = false;
                AuthStatusText.Text = "Opening browser for authentication...";

                var result = await _authService.AuthenticateAsync();

                if (result)
                {
                    AuthStatusText.Text = "Successfully authenticated!";
                    UpdateAuthUI();
                }
                else
                {
                    AuthStatusText.Text = "Authentication failed or was cancelled.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Authentication error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                AuthStatusText.Text = "Authentication error.";
            }
            finally
            {
                LoginButton.IsEnabled = true;
            }
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            _authService.Logout();
            AuthStatusText.Text = "Logged out successfully.";
            UpdateAuthUI();
        }

        private void UpdateAuthUI()
        {
            var isLoggedIn = _authService.IsAuthenticated();

            NotLoggedInPanel.Visibility = isLoggedIn ? Visibility.Collapsed : Visibility.Visible;
            LoggedInPanel.Visibility = isLoggedIn ? Visibility.Visible : Visibility.Collapsed;

            if (isLoggedIn)
            {
                UserEmailText.Text = _authService.GetUserEmail() ?? "Unknown";
            }
        }

        private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select default download folder",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DownloadFolderTextBox.Text = dialog.SelectedPath;
                AutoSaveSettings(); // Auto-save after changing folder
            }
        }

        private void ConnectionsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only auto-save if the window is loaded (not during initial setup)
            if (IsLoaded)
            {
                AutoSaveSettings();
            }
        }

        private void AutoSaveSettings()
        {
            try
            {
                var config = _configManager.LoadConfig();
                config.DownloadFolder = DownloadFolderTextBox.Text;

                if (ConnectionsComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int connections))
                {
                    config.MaxConnections = connections;
                }

                _configManager.SaveConfig(config);
            }
            catch (Exception ex)
            {
                // Silently fail - no need to bother user with save errors
                System.Diagnostics.Debug.WriteLine($"Error auto-saving settings: {ex.Message}");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Save settings one final time when closing
            AutoSaveSettings();
            base.OnClosing(e);
        }
    }
}
