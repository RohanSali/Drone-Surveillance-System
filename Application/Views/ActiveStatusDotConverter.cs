using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DroneSurveillanceSystem.Views
{
    public class ActiveStatusDotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var statusText = value as string;
                var isActive = !string.IsNullOrWhiteSpace(statusText) && !statusText.Equals("Disconnected", StringComparison.OrdinalIgnoreCase);
                return new SolidColorBrush(isActive ? Colors.Green : Colors.Red);
            }
            catch { return new SolidColorBrush(Colors.Red); }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


