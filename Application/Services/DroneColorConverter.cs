using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DroneSurveillanceSystem.Services
{
    public class DroneColorConverter : IValueConverter
    {
        private static readonly Brush[] DroneColors = new Brush[]
        {
            new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
            new SolidColorBrush(Color.FromRgb(255, 152, 0)),   // Orange
            new SolidColorBrush(Color.FromRgb(233, 30, 99)),   // Pink
            new SolidColorBrush(Color.FromRgb(156, 39, 176)),  // Purple
            new SolidColorBrush(Color.FromRgb(0, 188, 212)),   // Cyan
            new SolidColorBrush(Color.FromRgb(255, 87, 34)),   // Deep Orange
            new SolidColorBrush(Color.FromRgb(121, 85, 72)),   // Brown
            new SolidColorBrush(Color.FromRgb(158, 158, 158))  // Grey
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string droneName)
            {
                // Use the drone name to determine color
                int hash = droneName.GetHashCode();
                int colorIndex = Math.Abs(hash) % DroneColors.Length;
                return DroneColors[colorIndex];
            }
            
            if (value is int index)
            {
                return DroneColors[index % DroneColors.Length];
            }

            // Default color
            return DroneColors[0];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 