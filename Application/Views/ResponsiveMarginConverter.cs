using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DroneSurveillanceSystem.Views
{
    public class ResponsiveMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double responsiveMultiplier && parameter is string marginStr)
            {
                // Parse margin string (e.g., "10,5,10,5")
                var parts = marginStr.Split(',');
                if (parts.Length == 4 && 
                    double.TryParse(parts[0], out double left) &&
                    double.TryParse(parts[1], out double top) &&
                    double.TryParse(parts[2], out double right) &&
                    double.TryParse(parts[3], out double bottom))
                {
                    return new Thickness(
                        left * responsiveMultiplier,
                        top * responsiveMultiplier,
                        right * responsiveMultiplier,
                        bottom * responsiveMultiplier
                    );
                }
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
