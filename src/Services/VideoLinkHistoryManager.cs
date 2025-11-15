using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Services
{
    public class VideoLinkHistoryManager
    {
        private readonly string _historyFilePath;
        private List<VideoLinkHistoryItem> _history;

        public VideoLinkHistoryManager()
        {
            var configFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RobsYTDownloader");
            _historyFilePath = Path.Combine(configFolder, "video_link_history.json");
            _history = LoadHistory();
        }

        public List<VideoLinkHistoryItem> GetHistory()
        {
            return _history.OrderByDescending(h => h.LastAccessedDate).ToList();
        }

        public void AddOrUpdateVideoLink(VideoLinkHistoryItem item)
        {
            // Check if URL already exists
            var existing = _history.FirstOrDefault(h => h.Url == item.Url);

            if (existing != null)
            {
                // Update existing entry
                existing.Title = item.Title;
                existing.Formats = item.Formats;
                existing.FormatCount = item.FormatCount;
                existing.HighestQuality = item.HighestQuality;
                existing.ThumbnailUrl = item.ThumbnailUrl;
                existing.LastAccessedDate = DateTime.Now;
            }
            else
            {
                // Add new entry
                _history.Add(item);
            }

            _ = SaveHistoryAsync();
        }

        public void UpdateLastAccessedDate(string id)
        {
            var item = _history.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                item.LastAccessedDate = DateTime.Now;
                _ = SaveHistoryAsync();
            }
        }

        public void RemoveVideoLink(string id)
        {
            var item = _history.FirstOrDefault(h => h.Id == id);
            if (item != null)
            {
                _history.Remove(item);
                _ = SaveHistoryAsync();
            }
        }

        public void ClearHistory()
        {
            _history.Clear();
            _ = SaveHistoryAsync();
        }

        public VideoLinkHistoryItem? GetVideoLinkById(string id)
        {
            return _history.FirstOrDefault(h => h.Id == id);
        }

        private List<VideoLinkHistoryItem> LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var items = JsonConvert.DeserializeObject<List<VideoLinkHistoryItem>>(json) ?? new List<VideoLinkHistoryItem>();

                    // Update format count for loaded items
                    foreach (var item in items)
                    {
                        item.FormatCount = item.Formats?.Count ?? 0;
                        if (item.Formats != null && item.Formats.Count > 0)
                        {
                            // Find highest quality
                            var highestQuality = item.Formats
                                .OrderByDescending(f => GetQualityScore(f))
                                .FirstOrDefault();

                            if (highestQuality != null)
                            {
                                item.HighestQuality = highestQuality.Resolution ?? highestQuality.DisplayName;
                            }
                        }
                    }

                    return items;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading video link history: {ex.Message}");
            }

            return new List<VideoLinkHistoryItem>();
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
                System.Diagnostics.Debug.WriteLine($"Error saving video link history: {ex.Message}");
            }
        }

        private int GetQualityScore(VideoFormat format)
        {
            // Simple scoring based on resolution
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
