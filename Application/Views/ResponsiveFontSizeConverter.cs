using System;
using System.Globalization;
using System.Windows.Data;

namespace DroneSurveillanceSystem.Views
{
    public class ResponsiveFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double baseFontSize && parameter is string baseSizeStr && double.TryParse(baseSizeStr, out double baseSize))
            {
                // Scale font size based on the responsive font size multiplier
                return baseSize * baseFontSize;
            }
            return parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
