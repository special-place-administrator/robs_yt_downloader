using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class DownloadQueueManager
    {
        private readonly ObservableCollection<DownloadHistoryItem> _queue;
        private readonly DownloadManager _downloadManager;
        private readonly DependencyManager _dependencyManager;
        private readonly ConfigManager _configManager;
        private readonly int _maxConcurrentDownloads = 3;
        private int _activeDownloads = 0;
        private readonly SemaphoreSlim _semaphore;

        public ObservableCollection<DownloadHistoryItem> Queue => _queue;

        public DownloadQueueManager()
        {
            _queue = new ObservableCollection<DownloadHistoryItem>();
            _downloadManager = new DownloadManager();
            _dependencyManager = new DependencyManager();
            _configManager = new ConfigManager();
            _semaphore = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
        }

        public void AddToQueue(DownloadHistoryItem item)
        {
            item.DownloadStatus = DownloadStatus.Queued;
            item.CancellationTokenSource = new CancellationTokenSource();
            _queue.Add(item);

            // Start processing queue
            _ = ProcessQueueAsync();
        }

        private async Task ProcessQueueAsync()
        {
            var itemsToProcess = _queue
                .Where(i => i.DownloadStatus == DownloadStatus.Queued)
                .ToList();

            foreach (var item in itemsToProcess)
            {
                // Wait for available slot
                await _semaphore.WaitAsync();

                // Check if cancelled
                if (item.CancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    item.DownloadStatus = DownloadStatus.Cancelled;
                    _semaphore.Release();
                    continue;
                }

                // Start download
                _ = DownloadItemAsync(item);
            }
        }

        private async Task DownloadItemAsync(DownloadHistoryItem item)
        {
            try
            {
                Interlocked.Increment(ref _activeDownloads);
                item.DownloadStatus = DownloadStatus.Downloading;

                var ytDlpPath = _dependencyManager.GetToolPath("yt-dlp");
                if (string.IsNullOrEmpty(ytDlpPath))
                {
                    throw new Exception("yt-dlp is not installed");
                }

                var config = _configManager.LoadConfig();
                var downloadFolder = config.DownloadFolder;
                if (string.IsNullOrWhiteSpace(downloadFolder) || !Directory.Exists(downloadFolder))
                {
                    downloadFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                }

                var arguments = BuildDownloadArguments(item, downloadFolder);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ytDlpPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = downloadFolder
                };

                using var process = new Process { StartInfo = processStartInfo };
                item.Process = process;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        ParseProgress(item, e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        Debug.WriteLine($"yt-dlp error: {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(item.CancellationTokenSource?.Token ?? CancellationToken.None);

                if (item.CancellationTokenSource?.Token.IsCancellationRequested == true)
                {
                    item.DownloadStatus = DownloadStatus.Cancelled;
                }
                else if (process.ExitCode == 0)
                {
                    // Verify file exists and has content
                    if (File.Exists(item.FilePath))
                    {
                        var fileInfo = new FileInfo(item.FilePath);
                        item.FileSize = fileInfo.Length;

                        if (fileInfo.Length > 0)
                        {
                            item.DownloadStatus = DownloadStatus.Completed;
                            item.Progress = 100;
                        }
                        else
                        {
                            item.DownloadStatus = DownloadStatus.Failed;
                            item.ErrorMessage = "Downloaded file is empty (0 bytes)";
                        }
                    }
                    else
                    {
                        item.DownloadStatus = DownloadStatus.Failed;
                        item.ErrorMessage = "Download file not found";
                    }
                }
                else
                {
                    item.DownloadStatus = DownloadStatus.Failed;
                    item.ErrorMessage = $"yt-dlp exited with code {process.ExitCode}";
                }
            }
            catch (OperationCanceledException)
            {
                item.DownloadStatus = DownloadStatus.Cancelled;
            }
            catch (Exception ex)
            {
                item.DownloadStatus = DownloadStatus.Failed;
                item.ErrorMessage = ex.Message;
            }
            finally
            {
                item.Process = null;
                Interlocked.Decrement(ref _activeDownloads);
                _semaphore.Release();
            }
        }

        private string BuildDownloadArguments(DownloadHistoryItem item, string downloadFolder)
        {
            var args = new List<string>();

            // Format selection
            args.Add($"-f {item.FormatId}");

            // Cookies
            var cookiesPath = _configManager.GetCookiesFilePath();
            if (File.Exists(cookiesPath))
            {
                args.Add($"--cookies \"{cookiesPath}\"");
            }

            // Node.js runtime
            var nodePath = _dependencyManager.GetToolPath("node");
            if (!string.IsNullOrEmpty(nodePath))
            {
                args.Add("--js-runtimes node");
            }

            // External downloader
            var aria2cPath = _dependencyManager.GetToolPath("aria2c");
            if (!string.IsNullOrEmpty(aria2cPath))
            {
                var config = _configManager.LoadConfig();
                var maxConnections = config.MaxConnections > 0 ? config.MaxConnections : 16;
                args.Add("--external-downloader aria2c");
                args.Add($"--external-downloader-args \"aria2c:-x {maxConnections} -s {maxConnections} -k 1M\"");
            }

            // Output template
            args.Add($"-o \"%(title)s.%(ext)s\"");

            // Merge format for combined downloads
            if (item.FormatId.Contains("+"))
            {
                args.Add("--merge-output-format mkv");
            }

            // URL
            args.Add($"\"{item.Url}\"");

            // Newline after each progress update for easier parsing
            args.Add("--newline");

            return string.Join(" ", args);
        }

        private void ParseProgress(DownloadHistoryItem item, string output)
        {
            try
            {
                // Parse download progress from yt-dlp output
                // Example: [download]  45.2% of 123.45MiB at 1.23MiB/s ETA 00:42
                var progressMatch = Regex.Match(output, @"\[download\]\s+(\d+\.?\d*)%");
                if (progressMatch.Success && double.TryParse(progressMatch.Groups[1].Value, out double progress))
                {
                    item.Progress = progress;
                }

                var speedMatch = Regex.Match(output, @"at\s+([\d\.]+\w+/s)");
                if (speedMatch.Success)
                {
                    item.Speed = speedMatch.Groups[1].Value;
                }

                var etaMatch = Regex.Match(output, @"ETA\s+(\d+:\d+)");
                if (etaMatch.Success)
                {
                    item.ETA = etaMatch.Groups[1].Value;
                }

                // Detect if file path is mentioned
                var destinationMatch = Regex.Match(output, @"\[download\]\s+Destination:\s+(.+)");
                if (destinationMatch.Success)
                {
                    item.FilePath = destinationMatch.Groups[1].Value.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing progress: {ex.Message}");
            }
        }

        public void PauseDownload(string itemId)
        {
            var item = _queue.FirstOrDefault(i => i.Id == itemId);
            if (item != null && item.DownloadStatus == DownloadStatus.Downloading)
            {
                item.Process?.Kill();
                item.DownloadStatus = DownloadStatus.Paused;
            }
        }

        public void ResumeDownload(string itemId)
        {
            var item = _queue.FirstOrDefault(i => i.Id == itemId);
            if (item != null && item.DownloadStatus == DownloadStatus.Paused)
            {
                item.DownloadStatus = DownloadStatus.Queued;
                item.CancellationTokenSource = new CancellationTokenSource();
                _ = ProcessQueueAsync();
            }
        }

        public void CancelDownload(string itemId)
        {
            var item = _queue.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                item.CancellationTokenSource?.Cancel();
                item.Process?.Kill();
                item.DownloadStatus = DownloadStatus.Cancelled;
            }
        }

        public void RemoveDownload(string itemId)
        {
            var item = _queue.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                // Cancel if still downloading
                if (item.DownloadStatus == DownloadStatus.Downloading || item.DownloadStatus == DownloadStatus.Queued)
                {
                    CancelDownload(itemId);
                }

                _queue.Remove(item);
            }
        }
    }
}
