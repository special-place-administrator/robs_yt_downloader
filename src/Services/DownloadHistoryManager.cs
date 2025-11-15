using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class DownloadHistoryManager
    {
        private readonly string _historyFilePath;
        private List<DownloadHistoryItem> _history;

        public DownloadHistoryManager()
        {
            var configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobsYTDownloader");
            _historyFilePath = Path.Combine(configFolder, "download_history.json");
            _history = LoadHistory();
        }

        public List<DownloadHistoryItem> GetHistory()
        {
            return _history.OrderByDescending(h => h.DownloadDate).ToList();
        }

        public void AddDownload(DownloadHistoryItem item)
        {
            _history.Add(item);
            // Save asynchronously to avoid blocking UI thread
            _ = SaveHistoryAsync();
        }

        public void UpdateDownload(DownloadHistoryItem item)
        {
            var existing = _history.FirstOrDefault(h => h.FilePath == item.FilePath);
            if (existing != null)
            {
                existing.Status = item.Status;
                existing.ErrorMessage = item.ErrorMessage;
                existing.FileSize = item.FileSize;
                // Save asynchronously to avoid blocking UI thread
                _ = SaveHistoryAsync();
            }
        }

        public void RemoveDownload(DownloadHistoryItem item)
        {
            _history.Remove(item);
            // Save asynchronously to avoid blocking UI thread
            _ = SaveHistoryAsync();
        }

        public void ClearHistory()
        {
            _history.Clear();
            // Save asynchronously to avoid blocking UI thread
            _ = SaveHistoryAsync();
        }

        private List<DownloadHistoryItem> LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    return JsonConvert.DeserializeObject<List<DownloadHistoryItem>>(json) ?? new List<DownloadHistoryItem>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading download history: {ex.Message}");
            }

            return new List<DownloadHistoryItem>();
        }

        private async Task SaveHistoryAsync()
        {
            try
            {
                // Serialize on thread pool to avoid blocking
                var json = await Task.Run(() => JsonConvert.SerializeObject(_history, Formatting.Indented));

                // Write file asynchronously
                await File.WriteAllTextAsync(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving download history: {ex.Message}");
            }
        }
    }
}
