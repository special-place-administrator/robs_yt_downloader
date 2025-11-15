using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RobsYTDownloader.Models
{
    public class DownloadHistoryItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private DownloadStatus _downloadStatus = DownloadStatus.Queued;
        private double _progress = 0;
        private string _speed = string.Empty;
        private string _eta = string.Empty;
        private long _fileSize = 0;
        private string _statusText = "Queued";

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public string FormatId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;

        public long FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
            }
        }

        public DateTime DownloadDate { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // Queue-specific properties
        public DownloadStatus DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                _downloadStatus = value;
                UpdateStatusText();
                OnPropertyChanged();
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                UpdateStatusText();
                OnPropertyChanged();
            }
        }

        public string Speed
        {
            get => _speed;
            set
            {
                _speed = value;
                UpdateStatusText();
                OnPropertyChanged();
            }
        }

        public string ETA
        {
            get => _eta;
            set
            {
                _eta = value;
                UpdateStatusText();
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        // For backward compatibility
        public string Status
        {
            get => DownloadStatus.ToString();
            set
            {
                if (Enum.TryParse<DownloadStatus>(value, out var status))
                {
                    DownloadStatus = status;
                }
            }
        }

        // Internal process reference (not serialized)
        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public Process? Process { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        [Newtonsoft.Json.JsonIgnore]
        public CancellationTokenSource? CancellationTokenSource { get; set; }

        private void UpdateStatusText()
        {
            StatusText = DownloadStatus switch
            {
                DownloadStatus.Queued => "Queued",
                DownloadStatus.Downloading => $"{Progress:F1}% - {Speed} - ETA: {ETA}",
                DownloadStatus.Paused => $"Paused - {Progress:F1}%",
                DownloadStatus.Completed => "Completed",
                DownloadStatus.Failed => $"Failed - {ErrorMessage}",
                DownloadStatus.Cancelled => "Cancelled",
                _ => DownloadStatus.ToString()
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
