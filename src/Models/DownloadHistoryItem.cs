using System;

namespace RobsYTDownloader.Models
{
    public class DownloadHistoryItem
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public DateTime DownloadDate { get; set; }
        public string Status { get; set; } = "Completed"; // Completed, Failed, In Progress
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
