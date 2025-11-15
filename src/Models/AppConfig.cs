namespace RobsYTDownloader.Models
{
    public class AppConfig
    {
        public string? DownloadFolder { get; set; }
        public int MaxConnections { get; set; } = 16;
        public string? LastQuality { get; set; }
        public string? YtDlpPath { get; set; }
        public string? Aria2cPath { get; set; }
        public string? FfmpegPath { get; set; }
        public string? NodePath { get; set; }
        public bool UseSystemTheme { get; set; } = true;
    }
}
