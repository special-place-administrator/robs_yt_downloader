using System;
using System.Collections.Generic;
using System.IO;
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
        private readonly SettingsExportService _exportService;
        private List<DependencyStatus> _dependencies = new();

        public SettingsWindow()
        {
            InitializeComponent();
            _dependencyManager = new DependencyManager();
            _authService = GoogleAuthService.Instance;
            _configManager = new ConfigManager();
            _exportService = new SettingsExportService();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            await CheckDependencies();
            UpdateAuthUI();
            UpdateExportSize();
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

        // Export/Import functionality

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ExportButton.IsEnabled = false;
                BackupStatusText.Text = "Preparing export...";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Gray;

                // Show save file dialog
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    FileName = $"RobsYTDownloader_Settings_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".zip",
                    Filter = "Settings Backup (*.zip)|*.zip"
                };

                if (dialog.ShowDialog() == true)
                {
                    var result = await _exportService.ExportSettings(dialog.FileName);

                    if (result.Success)
                    {
                        BackupStatusText.Text = $"✓ {result.Message}";
                        BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;

                        var openResult = MessageBox.Show(
                            "Settings exported successfully!\n\nWould you like to open the folder containing the backup file?",
                            "Export Successful",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (openResult == MessageBoxResult.Yes)
                        {
                            var folderPath = Path.GetDirectoryName(dialog.FileName);
                            if (!string.IsNullOrEmpty(folderPath))
                            {
                                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dialog.FileName}\"");
                            }
                        }
                    }
                    else
                    {
                        BackupStatusText.Text = $"✗ {result.Message}";
                        BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
                        MessageBox.Show(result.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    BackupStatusText.Text = "Export cancelled.";
                    BackupStatusText.Foreground = System.Windows.Media.Brushes.Gray;
                }
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✗ Export error: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ExportButton.IsEnabled = true;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show warning
                var confirmResult = MessageBox.Show(
                    "Import will replace your current settings with those from the backup file.\n\n" +
                    "Your current settings will be automatically backed up before importing.\n\n" +
                    "You will need to restart the application after importing.\n\n" +
                    "Do you want to continue?",
                    "Confirm Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    return;
                }

                // Show open file dialog
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    DefaultExt = ".zip",
                    Filter = "Settings Backup (*.zip)|*.zip"
                };

                if (dialog.ShowDialog() == true)
                {
                    ImportButton.IsEnabled = false;
                    BackupStatusText.Text = "Importing settings...";
                    BackupStatusText.Foreground = System.Windows.Media.Brushes.Gray;

                    var result = await _exportService.ImportSettings(dialog.FileName);

                    if (result.Success)
                    {
                        BackupStatusText.Text = $"✓ {result.Message}";
                        BackupStatusText.Foreground = System.Windows.Media.Brushes.Green;

                        MessageBox.Show(
                            result.Message,
                            "Import Successful",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Ask if user wants to restart now
                        var restartResult = MessageBox.Show(
                            "Would you like to close the application now to apply the imported settings?",
                            "Restart Required",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (restartResult == MessageBoxResult.Yes)
                        {
                            Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        BackupStatusText.Text = $"✗ {result.Message}";
                        BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
                        MessageBox.Show(result.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    ImportButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                BackupStatusText.Text = $"✗ Import error: {ex.Message}";
                BackupStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show($"Import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ImportButton.IsEnabled = true;
            }
        }

        private void UpdateExportSize()
        {
            try
            {
                var sizeBytes = _exportService.GetExportableDataSize();
                var sizeKB = sizeBytes / 1024.0;

                string sizeText;
                if (sizeKB < 1)
                {
                    sizeText = $"Estimated size: {sizeBytes} bytes";
                }
                else if (sizeKB < 1024)
                {
                    sizeText = $"Estimated size: {sizeKB:F1} KB";
                }
                else
                {
                    var sizeMB = sizeKB / 1024.0;
                    sizeText = $"Estimated size: {sizeMB:F2} MB";
                }

                ExportSizeText.Text = sizeText;
            }
            catch
            {
                ExportSizeText.Text = "Estimated size: Unknown";
            }
        }
    }
}
