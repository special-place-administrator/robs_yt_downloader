namespace RobsYTDownloader.Models
{
    public class DependencyStatus
    {
        public string Name { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string StatusIcon => IsInstalled ? "✓" : "✗";
        public string ButtonText => IsInstalled ? "Update" : "Install";
        public bool CanInstall => !IsInstalling;
        public bool IsInstalling { get; set; }
        public string? Version { get; set; }
        public string? Path { get; set; }
    }
}
