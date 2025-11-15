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
        private readonly DownloadQueueManager _queueManager;
        private readonly ConfigManager _configManager;
        private readonly DownloadHistoryManager _historyManager;
        private readonly VideoLinkHistoryManager _videoLinkHistoryManager;
        private VideoInfo? _currentVideo;
        private List<VideoFormat> _allFormats = new(); // Store ALL formats
        private string _baseVideoTitle = string.Empty; // Store base title without quality suffix

        public MainWindow()
        {
            InitializeComponent();
            _downloadManager = new DownloadManager();
            _queueManager = new DownloadQueueManager();
            _configManager = new ConfigManager();
            _historyManager = new DownloadHistoryManager();
            _videoLinkHistoryManager = new VideoLinkHistoryManager();

            _downloadManager.ProgressChanged += OnProgressChanged;
            _downloadManager.StatusChanged += OnStatusChanged;

            // Bind history tables
            HistoryDataGrid.ItemsSource = _queueManager.Queue;
            VideoLinkHistoryDataGrid.ItemsSource = _videoLinkHistoryManager.GetHistory();

            LoadConfig();
            LoadHistoricalDownloads();
            UpdateVideoLinkHistoryEmptyState();
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

        private void LoadHistoricalDownloads()
        {
            // Load completed downloads from history into the queue view
            var history = _historyManager.GetHistory();
            foreach (var item in history)
            {
                // Only show completed/failed downloads from history, not active ones
                if (item.DownloadStatus == DownloadStatus.Completed || item.DownloadStatus == DownloadStatus.Failed)
                {
                    _queueManager.Queue.Add(item);
                }
            }

            UpdateEmptyState();
        }

        private void RefreshHistory()
        {
            // Queue updates automatically via ObservableCollection
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            // Show/hide empty state for download history
            if (_queueManager.Queue.Count == 0)
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

        private void UpdateVideoLinkHistoryEmptyState()
        {
            // Show/hide empty state for video link history
            var history = _videoLinkHistoryManager.GetHistory();
            if (history.Count == 0)
            {
                EmptyVideoLinkHistoryPanel.Visibility = Visibility.Visible;
                VideoLinkHistoryDataGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyVideoLinkHistoryPanel.Visibility = Visibility.Collapsed;
                VideoLinkHistoryDataGrid.Visibility = Visibility.Visible;
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
                _baseVideoTitle = _currentVideo.Title; // Store base title

                // Save to video link history
                var historyItem = new VideoLinkHistoryItem
                {
                    Url = sanitizedUrl,
                    Title = _currentVideo.Title,
                    Formats = _allFormats,
                    FormatCount = _allFormats.Count,
                    FetchDate = DateTime.Now,
                    HighestQuality = _allFormats.OrderByDescending(f => GetQualityScore(f)).FirstOrDefault()?.Resolution ?? ""
                };
                _videoLinkHistoryManager.AddOrUpdateVideoLink(historyItem);

                // Update video link history display
                VideoLinkHistoryDataGrid.ItemsSource = _videoLinkHistoryManager.GetHistory();
                UpdateVideoLinkHistoryEmptyState();

                // Apply current filter
                ApplyFormatFilter();

                // Update video name display
                VideoNameText.Text = _currentVideo.Title;
                VideoNameText.Foreground = Brushes.White;

                QualityComboBox.IsEnabled = true;
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

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
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
                var outputPath = Path.Combine(downloadFolder, fileName);

                // Check if file already exists and append number if needed
                var counter = 1;
                while (File.Exists(outputPath))
                {
                    fileName = $"{sanitizedTitle} [{selectedFormat.Resolution}] ({counter}).{extension}";
                    outputPath = Path.Combine(downloadFolder, fileName);
                    counter++;
                }

                // Create download item and add to queue
                var downloadItem = new DownloadHistoryItem
                {
                    Url = sanitizedUrl,
                    Title = _currentVideo.Title,
                    Quality = selectedFormat.DisplayName,
                    FormatId = selectedFormat.FormatId,
                    FilePath = outputPath,
                    DownloadDate = DateTime.Now,
                    DownloadStatus = DownloadStatus.Queued
                };

                // Add to queue - it will start automatically
                _queueManager.AddToQueue(downloadItem);

                // Also save to history manager for persistence
                _historyManager.AddDownload(downloadItem);

                UpdateEmptyState();
                ShowNotification($"Added to download queue: {_currentVideo.Title}", NotificationSeverity.Success);
                StatusText.Text = $"Added to queue - {_queueManager.Queue.Count(i => i.DownloadStatus == DownloadStatus.Queued || i.DownloadStatus == DownloadStatus.Downloading)} active download(s)";
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to add to queue: {ex.Message}", NotificationSeverity.Error);
                StatusText.Text = "Failed to add to queue";
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

                        _queueManager.RemoveDownload(item.Id);
                        _historyManager.RemoveDownload(item);
                        UpdateEmptyState();
                        ShowNotification("File deleted successfully.", NotificationSeverity.Success);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification($"Failed to delete file: {ex.Message}", NotificationSeverity.Error);
                    }
                }
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null)
            {
                _queueManager.PauseDownload(item.Id);
                ShowNotification($"Paused: {item.Title}", NotificationSeverity.Information);
            }
        }

        private void ResumeButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null)
            {
                _queueManager.ResumeDownload(item.Id);
                ShowNotification($"Resumed: {item.Title}", NotificationSeverity.Information);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            var item = button?.Tag as DownloadHistoryItem;

            if (item != null)
            {
                var result = System.Windows.MessageBox.Show(
                    $"Are you sure you want to cancel this download?\n\n{item.Title}",
                    "Confirm Cancel",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _queueManager.CancelDownload(item.Id);
                    ShowNotification($"Cancelled: {item.Title}", NotificationSeverity.Information);
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

        // Format Filtering with Toggle Buttons
        private void FilterToggle_Click(object sender, RoutedEventArgs e)
        {
            var clicked = sender as System.Windows.Controls.Primitives.ToggleButton;
            if (clicked == null) return;

            // Ensure only one toggle is checked at a time
            if (clicked.IsChecked == true)
            {
                if (clicked != FilterAllToggle) FilterAllToggle.IsChecked = false;
                if (clicked != FilterVideoAudioToggle) FilterVideoAudioToggle.IsChecked = false;
                if (clicked != FilterVideoOnlyToggle) FilterVideoOnlyToggle.IsChecked = false;
                if (clicked != FilterAudioOnlyToggle) FilterAudioOnlyToggle.IsChecked = false;

                clicked.IsChecked = true; // Keep clicked one checked
            }
            else
            {
                // Don't allow unchecking - keep at least one checked
                clicked.IsChecked = true;
                return;
            }

            // Apply filter if we have formats loaded
            if (_allFormats.Count > 0)
            {
                ApplyFormatFilter();
            }
        }

        private void ApplyFormatFilter()
        {
            if (_allFormats.Count == 0) return;

            List<VideoFormat> filteredFormats;

            if (FilterAllToggle.IsChecked == true)
            {
                filteredFormats = _allFormats;
            }
            else if (FilterVideoAudioToggle.IsChecked == true)
            {
                filteredFormats = _allFormats.Where(f =>
                    f.VideoCodec != "none" && f.AudioCodec != "none"
                ).ToList();
            }
            else if (FilterVideoOnlyToggle.IsChecked == true)
            {
                filteredFormats = _allFormats.Where(f =>
                    f.VideoCodec != "none" && f.AudioCodec == "none"
                ).ToList();
            }
            else if (FilterAudioOnlyToggle.IsChecked == true)
            {
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

            var filterName = FilterAllToggle.IsChecked == true ? "all" :
                            FilterVideoAudioToggle.IsChecked == true ? "video+audio" :
                            FilterVideoOnlyToggle.IsChecked == true ? "video-only" :
                            "audio-only";

            StatusText.Text = $"Showing {filteredFormats.Count} {filterName} formats (of {_allFormats.Count} total)";
        }

        // Quality Selection Changed - Update video name with quality suffix
        private void QualityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityComboBox.SelectedItem is VideoFormat selectedFormat && !string.IsNullOrEmpty(_baseVideoTitle))
            {
                // Update video name with quality info
                var qualityInfo = selectedFormat.DisplayName;
                VideoNameText.Text = $"{_baseVideoTitle} - {qualityInfo}";
                VideoNameText.Foreground = Brushes.White;

                // Enable download button when quality selected
                DownloadButton.IsEnabled = true;
            }
        }

        // Video Link History Button Handlers
        private void UseVideoLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as VideoLinkHistoryItem;
            if (item == null) return;

            // Update last accessed date
            _videoLinkHistoryManager.UpdateLastAccessedDate(item.Id);

            // Populate UI with stored data
            UrlTextBox.Text = item.Url;
            _currentVideo = new VideoInfo
            {
                Title = item.Title,
                Formats = item.Formats
            };
            _allFormats = item.Formats;
            _baseVideoTitle = item.Title;

            // Update video name
            VideoNameText.Text = item.Title;
            VideoNameText.Foreground = Brushes.White;

            // Apply filter and populate dropdown
            ApplyFormatFilter();

            QualityComboBox.IsEnabled = true;
            StatusText.Text = $"Loaded {item.FormatCount} formats from history";
            ShowNotification($"Loaded: {item.Title}", NotificationSeverity.Success);
        }

        private void CopyVideoLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as VideoLinkHistoryItem;
            if (item == null) return;

            try
            {
                Clipboard.SetText(item.Url);
                ShowNotification("URL copied to clipboard!", NotificationSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowNotification($"Failed to copy URL: {ex.Message}", NotificationSeverity.Error);
            }
        }

        private void RemoveVideoLinkButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button?.Tag as VideoLinkHistoryItem;
            if (item == null) return;

            var result = MessageBox.Show(
                $"Remove this video from history?\n\n{item.Title}",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _videoLinkHistoryManager.RemoveVideoLink(item.Id);
                VideoLinkHistoryDataGrid.ItemsSource = _videoLinkHistoryManager.GetHistory();
                UpdateVideoLinkHistoryEmptyState();
                ShowNotification("Video link removed from history", NotificationSeverity.Information);
            }
        }

        // Helper method for quality scoring
        private int GetQualityScore(VideoFormat format)
        {
            if (string.IsNullOrEmpty(format.Resolution)) return 0;

            var resolution = format.Resolution.ToLower();
            if (resolution.Contains("8k") || resolution.Contains("4320")) return 8000;
            if (resolution.Contains("5k") || resolution.Contains("2880")) return 5000;
            if (resolution.Contains("4k") || resolution.Contains("2160")) return 4000;
            if (resolution.Contains("1440")) return 2000;
            if (resolution.Contains("1080")) return 1080;
            if (resolution.Contains("720")) return 720;
            if (resolution.Contains("480")) return 480;
            if (resolution.Contains("360")) return 360;
            if (resolution.Contains("240")) return 240;

            return 0;
        }
    }
}
