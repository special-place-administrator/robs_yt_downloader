using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RobsYTDownloader.Models;
using RobsYTDownloader.Services;

namespace RobsYTDownloader
{
    public partial class MainWindow : Window
    {
        private readonly DownloadManager _downloadManager;
        private readonly ConfigManager _configManager;
        private readonly DownloadHistoryManager _historyManager;
        private VideoInfo? _currentVideo;
        private List<VideoFormat> _allFormats = new(); // Store ALL formats

        public MainWindow()
        {
            InitializeComponent();
            _downloadManager = new DownloadManager();
            _configManager = new ConfigManager();
            _historyManager = new DownloadHistoryManager();

            _downloadManager.ProgressChanged += OnProgressChanged;
            _downloadManager.StatusChanged += OnStatusChanged;

            LoadConfig();
            RefreshHistory();
        }

        private void LoadConfig()
        {
            var config = _configManager.LoadConfig();
            // Apply saved configuration if needed
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Bring window to front when activated
            Topmost = true;
            Topmost = false;
        }

        private void RefreshHistory()
        {
            var history = _historyManager.GetHistory();
            HistoryDataGrid.ItemsSource = history;

            // Show/hide empty state
            if (history.Count == 0)
            {
                EmptyHistoryPanel.Visibility = Visibility.Visible;
                HistoryDataGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyHistoryPanel.Visibility = Visibility.Collapsed;
                HistoryDataGrid.Visibility = Visibility.Visible;
            }
        }

        private enum NotificationSeverity
        {
            Success,
            Error,
            Warning,
            Information
        }

        private void ShowNotification(string message, NotificationSeverity severity)
        {
            NotificationTitle.Text = severity == NotificationSeverity.Success ? "Success" :
                                     severity == NotificationSeverity.Error ? "Error" :
                                     severity == NotificationSeverity.Warning ? "Warning" : "Information";
            NotificationMessage.Text = message;

            // Set colors based on severity
            var successColor = new SolidColorBrush(Color.FromRgb(16, 124, 16));
            var errorColor = new SolidColorBrush(Color.FromRgb(196, 43, 28));
            var warningColor = new SolidColorBrush(Color.FromRgb(157, 93, 0));
            var infoColor = new SolidColorBrush(Color.FromRgb(0, 90, 158));

            switch (severity)
            {
                case NotificationSeverity.Success:
                    NotificationBar.Background = new SolidColorBrush(Color.FromArgb(40, 16, 124, 16));
                    NotificationBar.BorderBrush = successColor;
                    NotificationIcon.Glyph = "\uE8FB"; // CheckMark
                    NotificationIcon.Foreground = successColor;
                    break;
                case NotificationSeverity.Error:
                    NotificationBar.Background = new SolidColorBrush(Color.FromArgb(40, 196, 43, 28));
                    NotificationBar.BorderBrush = errorColor;
                    NotificationIcon.Glyph = "\uEA39"; // ErrorBadge
                    NotificationIcon.Foreground = errorColor;
                    break;
                case NotificationSeverity.Warning:
                    NotificationBar.Background = new SolidColorBrush(Color.FromArgb(40, 157, 93, 0));
                    NotificationBar.BorderBrush = warningColor;
                    NotificationIcon.Glyph = "\uE7BA"; // Warning
                    NotificationIcon.Foreground = warningColor;
                    break;
                case NotificationSeverity.Information:
                    NotificationBar.Background = new SolidColorBrush(Color.FromArgb(40, 0, 90, 158));
                    NotificationBar.BorderBrush = infoColor;
                    NotificationIcon.Glyph = "\uE946"; // Info
                    NotificationIcon.Foreground = infoColor;
                    break;
            }

            NotificationBar.BorderThickness = new Thickness(1);
            NotificationBar.Visibility = Visibility.Visible;

            // Auto-close success messages after 5 seconds
            if (severity == NotificationSeverity.Success)
            {
                Task.Delay(5000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => NotificationBar.Visibility = Visibility.Collapsed);
                });
            }
        }

        private void CloseNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationBar.Visibility = Visibility.Collapsed;
        }

        private void UrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FetchButton.IsEnabled = !string.IsNullOrWhiteSpace(UrlTextBox.Text);
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                ShowNotification("Please enter a YouTube URL.", NotificationSeverity.Warning);
                return;
            }

            // Sanitize URL to extract video ID and remove playlist/extra parameters
            var sanitizedUrl = SanitizeYouTubeUrl(url);
            if (sanitizedUrl == null)
            {
                ShowNotification("Invalid YouTube URL format.", NotificationSeverity.Warning);
                return;
            }

            try
            {
                // Show loading overlay
                ShowLoading("Connecting to YouTube...");

                FetchButton.IsEnabled = false;
                DownloadButton.IsEnabled = false;
                QualityComboBox.IsEnabled = false;
                UrlTextBox.IsEnabled = false;

                // Run on background thread to prevent UI freeze
                _currentVideo = await Task.Run(async () => await _downloadManager.FetchFormats(sanitizedUrl)).ConfigureAwait(true);

                HideLoading();

                if (_currentVideo == null || _currentVideo.Formats.Count == 0)
                {
                    ShowNotification("No formats found for this video.\n\nPlease check:\n• The URL is correct\n• The video is publicly accessible", NotificationSeverity.Warning);
                    StatusText.Text = "No formats found";
                    return;
                }

                // Store ALL formats
                _allFormats = _currentVideo.Formats;

                // Apply current filter
                ApplyFormatFilter();

                QualityComboBox.IsEnabled = true;
                DownloadButton.IsEnabled = true;
                StatusText.Text = $"✓ Found {_allFormats.Count} quality options for \"{_currentVideo.Title}\"";
                ShowNotification($"Successfully fetched {_allFormats.Count} quality options!", NotificationSeverity.Success);
            }
            catch (Exception ex)
            {
                HideLoading();
                ShowNotification(ex.Message, NotificationSeverity.Error);
                StatusText.Text = "Failed to fetch formats";
            }
            finally
            {
                FetchButton.IsEnabled = true;
                UrlTextBox.IsEnabled = true;
            }
        }

        private void ShowLoading(string message)
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
            LoadingSpinner.IsActive = true;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingSpinner.IsActive = false;
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();

            // Sanitize URL before downloading
            var sanitizedUrl = SanitizeYouTubeUrl(url);
            if (sanitizedUrl == null)
            {
                ShowNotification("Invalid YouTube URL format.", NotificationSeverity.Warning);
                return;
            }

            var selectedFormat = QualityComboBox.SelectedItem as VideoFormat;

            if (selectedFormat == null)
            {
                ShowNotification("Please select a quality.", NotificationSeverity.Warning);
                return;
            }

            if (_currentVideo == null)
            {
                ShowNotification("Please fetch video information first.", NotificationSeverity.Warning);
                return;
            }

            string outputPath = string.Empty;

            try
            {
                // Get configured download folder
                var config = _configManager.LoadConfig();
                var downloadFolder = config.DownloadFolder;

                // Use default Downloads folder if not configured
                if (string.IsNullOrWhiteSpace(downloadFolder) || !Directory.Exists(downloadFolder))
                {
                    downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                    // Ensure the directory exists
                    if (!Directory.Exists(downloadFolder))
                    {
                        Directory.CreateDirectory(downloadFolder);
                    }
                }

                // Sanitize video title for filename
                var sanitizedTitle = SanitizeFileName(_currentVideo.Title);

                // Determine file extension from format
                var extension = selectedFormat.Extension;
                if (selectedFormat.FormatId.Contains("+"))
                {
                    // For merged formats, use mkv by default
                    extension = "mkv";
                }

                // Generate filename: "VideoTitle [Resolution].ext"
                var fileName = $"{sanitizedTitle} [{selectedFormat.Resolution}].{extension}";
                outputPath = Path.Combine(downloadFolder, fileName);

                // Check if file already exists and append number if needed
                var counter = 1;
                while (File.Exists(outputPath))
                {
                    fileName = $"{sanitizedTitle} [{selectedFormat.Resolution}] ({counter}).{extension}";
                    outputPath = Path.Combine(downloadFolder, fileName);
                    counter++;
                }

                // Create history item
                var historyItem = new DownloadHistoryItem
                {
                    Url = sanitizedUrl,
                    Title = _currentVideo.Title,
                    Quality = selectedFormat.DisplayName,
                    FilePath = outputPath,
                    DownloadDate = DateTime.Now,
                    Status = "In Progress"
                };

                _historyManager.AddDownload(historyItem);
                RefreshHistory();

                ShowLoading("Starting download...");
                DownloadButton.IsEnabled = false;
                FetchButton.IsEnabled = false;
                UrlTextBox.IsEnabled = false;
                QualityComboBox.IsEnabled = false;

                // Run on background thread to prevent UI freeze
                await Task.Run(async () => await _downloadManager.DownloadVideo(sanitizedUrl, selectedFormat.FormatId, outputPath)).ConfigureAwait(true);

                HideLoading();

                // Update history item
                historyItem.Status = "Completed";
                if (File.Exists(outputPath))
                {
                    historyItem.FileSize = new FileInfo(outputPath).Length;
                }
                _historyManager.UpdateDownload(historyItem);
                RefreshHistory();

                ShowNotification($"Download completed successfully!\n\nSaved to: {Path.GetFileName(outputPath)}", NotificationSeverity.Success);
                StatusText.Text = "Download completed";
                DownloadProgress.Value = 0;
                ProgressText.Text = "";
            }
            catch (Exception ex)
            {
                HideLoading();

                // Update history item as failed
                if (!string.IsNullOrEmpty(outputPath))
                {
                    var failedItem = _historyManager.GetHistory().FirstOrDefault(h => h.FilePath == outputPath);
                    if (failedItem != null)
                    {
                        failedItem.Status = "Failed";
                        failedItem.ErrorMessage = ex.Message;
                        _historyManager.UpdateDownload(failedItem);
                        RefreshHistory();
                    }
                }

                ShowNotification($"Download failed: {ex.Message}", NotificationSeverity.Error);
                StatusText.Text = "Download failed";
            }
            finally
            {
                DownloadButton.IsEnabled = true;
                FetchButton.IsEnabled = true;
                UrlTextBox.IsEnabled = true;
                QualityComboBox.IsEnabled = true;
            }
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid filename characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Trim and limit length
            sanitized = sanitized.Trim();
            if (sanitized.Length > 150)
            {
                sanitized = sanitized.Substring(0, 150).Trim();
            }

            return sanitized;
        }

        private string? SanitizeYouTubeUrl(string url)
        {
            // Extract video ID from various YouTube URL formats
            // Supports:
            // - https://www.youtube.com/watch?v=VIDEO_ID
            // - https://www.youtube.com/watch?v=VIDEO_ID&list=PLAYLIST_ID
            // - https://youtu.be/VIDEO_ID
            // - https://m.youtube.com/watch?v=VIDEO_ID
            // - http://youtube.com/watch?v=VIDEO_ID
            // And more variations

            string? videoId = null;

            // Pattern 1: Standard watch URLs (with or without query parameters)
            var watchMatch = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})");
            if (watchMatch.Success)
            {
                videoId = watchMatch.Groups[1].Value;
            }

            // Pattern 2: Short URLs (youtu.be)
            if (videoId == null)
            {
                var shortMatch = Regex.Match(url, @"youtu\.be/([a-zA-Z0-9_-]{11})");
                if (shortMatch.Success)
                {
                    videoId = shortMatch.Groups[1].Value;
                }
            }

            // Pattern 3: Embed URLs
            if (videoId == null)
            {
                var embedMatch = Regex.Match(url, @"youtube\.com/embed/([a-zA-Z0-9_-]{11})");
                if (embedMatch.Success)
                {
                    videoId = embedMatch.Groups[1].Value;
                }
            }

            // If video ID was found, construct clean URL
            if (!string.IsNullOrEmpty(videoId))
            {
                return $"https://www.youtube.com/watch?v={videoId}";
            }

            // If no pattern matched, return original URL (might be valid as-is)
            return url;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow
            {
                Owner = this
            };
            settingsWindow.ShowDialog();
        }

        private void OnProgressChanged(object? sender, DownloadProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                DownloadProgress.Value = e.Percentage;
                ProgressText.Text = $"{e.Percentage:F1}% - {e.Speed} - ETA: {e.ETA}";
            });
        }

        private void OnStatusChanged(object? sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = status;

                // Update loading overlay text if visible
                if (LoadingOverlay.Visibility == Visibility.Visible)
                {
                    LoadingText.Text = status;
                }
            });
        }

        // History Action Handlers
        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null && File.Exists(item.FilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = item.FilePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    ShowNotification($"Failed to open file: {ex.Message}", NotificationSeverity.Error);
                }
            }
            else
            {
                ShowNotification("File not found. It may have been moved or deleted.", NotificationSeverity.Warning);
            }
        }

        private void ShowInFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null && File.Exists(item.FilePath))
            {
                try
                {
                    Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                }
                catch (Exception ex)
                {
                    ShowNotification($"Failed to open folder: {ex.Message}", NotificationSeverity.Error);
                }
            }
            else
            {
                ShowNotification("File not found. It may have been moved or deleted.", NotificationSeverity.Warning);
            }
        }

        private void DeleteFileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to delete this file?\n\n{Path.GetFileName(item.FilePath)}",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (File.Exists(item.FilePath))
                        {
                            File.Delete(item.FilePath);
                        }

                        _historyManager.RemoveDownload(item);
                        RefreshHistory();
                        ShowNotification("File deleted successfully.", NotificationSeverity.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"Failed to delete file: {ex.Message}", NotificationSeverity.Error);
                    }
                }
            }
        }

        private void OpenDownloadFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var config = _configManager.LoadConfig();
            var downloadFolder = config.DownloadFolder;

            if (string.IsNullOrWhiteSpace(downloadFolder) || !Directory.Exists(downloadFolder))
            {
                downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }

            if (Directory.Exists(downloadFolder))
            {
                Process.Start("explorer.exe", downloadFolder);
            }
            else
            {
                ShowNotification("Download folder not found.", NotificationSeverity.Warning);
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_historyManager.GetHistory().Count == 0)
            {
                ShowNotification("History is already empty.", NotificationSeverity.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to clear all download history?\n\nThis will not delete the actual files, only the history records.",
                "Confirm Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _historyManager.ClearHistory();
                RefreshHistory();
                ShowNotification("History cleared successfully.", NotificationSeverity.Success);
            }
        }

        // Format Filtering
        private void FilterRadio_Checked(object sender, RoutedEventArgs e)
        {
            // Only apply filter if we have formats loaded
            if (_allFormats.Count > 0)
            {
                ApplyFormatFilter();
            }
        }

        private void ApplyFormatFilter()
        {
            if (_allFormats.Count == 0) return;

            List<VideoFormat> filteredFormats;

            if (FilterAllRadio.IsChecked == true)
            {
                // Show all formats
                filteredFormats = _allFormats;
            }
            else if (FilterVideoAudioRadio.IsChecked == true)
            {
                // Show only formats with both video and audio
                filteredFormats = _allFormats.Where(f =>
                    f.VideoCodec != "none" && f.AudioCodec != "none"
                ).ToList();
            }
            else if (FilterVideoOnlyRadio.IsChecked == true)
            {
                // Show only video-only formats (no audio)
                filteredFormats = _allFormats.Where(f =>
                    f.VideoCodec != "none" && f.AudioCodec == "none"
                ).ToList();
            }
            else if (FilterAudioOnlyRadio.IsChecked == true)
            {
                // Show only audio-only formats (no video)
                filteredFormats = _allFormats.Where(f =>
                    f.VideoCodec == "none" && f.AudioCodec != "none"
                ).ToList();
            }
            else
            {
                filteredFormats = _allFormats;
            }

            QualityComboBox.ItemsSource = filteredFormats;
            if (filteredFormats.Count > 0)
            {
                QualityComboBox.SelectedIndex = 0;
            }

            // Update status text
            var filterName = FilterAllRadio.IsChecked == true ? "all" :
                            FilterVideoAudioRadio.IsChecked == true ? "video+audio" :
                            FilterVideoOnlyRadio.IsChecked == true ? "video-only" :
                            "audio-only";

            StatusText.Text = $"Showing {filteredFormats.Count} {filterName} formats (of {_allFormats.Count} total)";
        }
    }
}
