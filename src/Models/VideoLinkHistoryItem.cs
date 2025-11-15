using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RobsYTDownloader.Models
{
    public class VideoLinkHistoryItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _url = string.Empty;
        private string _title = string.Empty;
        private string _thumbnailUrl = string.Empty;
        private DateTime _fetchDate = DateTime.Now;
        private DateTime _lastAccessedDate = DateTime.Now;
        private int _formatCount = 0;
        private string _highestQuality = string.Empty;

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                OnPropertyChanged();
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string ThumbnailUrl
        {
            get => _thumbnailUrl;
            set
            {
                _thumbnailUrl = value;
                OnPropertyChanged();
            }
        }

        public List<VideoFormat> Formats { get; set; } = new();

        public DateTime FetchDate
        {
            get => _fetchDate;
            set
            {
                _fetchDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FetchDateFormatted));
            }
        }

        public DateTime LastAccessedDate
        {
            get => _lastAccessedDate;
            set
            {
                _lastAccessedDate = value;
                OnPropertyChanged();
            }
        }

        public int FormatCount
        {
            get => _formatCount;
            set
            {
                _formatCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QualityText));
            }
        }

        public string HighestQuality
        {
            get => _highestQuality;
            set
            {
                _highestQuality = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QualityText));
            }
        }

        // Display properties
        public string FetchDateFormatted => FetchDate.ToString("yyyy-MM-dd HH:mm");

        public string QualityText => $"{FormatCount} formats";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
