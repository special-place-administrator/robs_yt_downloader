namespace RobsYTDownloader.Models
{
    public class VideoFormat
    {
        public string FormatId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string VideoCodec { get; set; } = string.Empty;
        public string AudioCodec { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public double? Fps { get; set; }
        public string? Hdr { get; set; }
    }
}
