using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DroneSurveillanceSystem.Views
{
    public class BoolStatusDotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var isOnline = value is bool b && b;
                return new SolidColorBrush(isOnline ? Colors.Green : Colors.Red);
            }
            catch { return new SolidColorBrush(Colors.Red); }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


