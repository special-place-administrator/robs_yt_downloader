using System.Collections.Generic;

namespace RobsYTDownloader.Models
{
    public class VideoInfo
    {
        public string Title { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
        public List<VideoFormat> Formats { get; set; } = new();
    }
}
