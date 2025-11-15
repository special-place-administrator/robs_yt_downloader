using System.Windows;
using ModernWpf;

namespace RobsYTDownloader
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Auto-detect Windows theme
            ThemeManager.Current.ApplicationTheme = null; // null = use system theme
        }
    }
}
