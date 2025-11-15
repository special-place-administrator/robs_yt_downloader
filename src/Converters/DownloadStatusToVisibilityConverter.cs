using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using RobsYTDownloader.Models;

namespace RobsYTDownloader.Converters
{
    public class DownloadStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DownloadStatus status && parameter is string allowedStates)
            {
                // Parameter format: "Downloading|Queued|Paused"
                var states = allowedStates.Split('|');
                var statusString = status.ToString();

                return states.Contains(statusString) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
