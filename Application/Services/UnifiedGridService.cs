using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Services
{
    public static class UnifiedGridService
    {
        // Unified grid configuration
        public const int GRID_SPACING = 50; // Consistent 50px spacing
        public const int DRONE_SIZE = 30; // Consistent drone size
        public const int CENTER_MARKER_SIZE = 8; // Consistent center marker
        
        // Grid colors
        public static readonly Color GRID_COLOR = Color.FromRgb(51, 51, 51);
        public static readonly Color CENTER_MARKER_COLOR = Colors.Red;
        public static readonly Color CENTER_MARKER_STROKE = Colors.White;
        
        // Drone colors
        public static readonly Color DRONE_BACKGROUND = Color.FromRgb(0, 120, 212); // #0078D4
        public static readonly Color DRONE_BORDER = Colors.White;
        public static readonly Color DRONE_ICON_COLOR = Color.FromRgb(0, 188, 212); // #00BCD4

        /// <summary>
        /// Draws a unified grid on the specified canvas
        /// </summary>
        public static void DrawUnifiedGrid(Canvas canvas, List<UIElement> gridElements)
        {
            if (canvas == null) return;

            // Clear existing grid elements
            foreach (var element in gridElements.ToList())
            {
                canvas.Children.Remove(element);
            }
            gridElements.Clear();

            var canvasWidth = canvas.ActualWidth > 0 ? canvas.ActualWidth : 1000;
            var canvasHeight = canvas.ActualHeight > 0 ? canvas.ActualHeight : 600;

            // Vertical grid lines
            for (int x = 0; x < canvasWidth; x += GRID_SPACING)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Stroke = new SolidColorBrush(GRID_COLOR),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
                gridElements.Add(line);
            }

            // Horizontal grid lines
            for (int y = 0; y < canvasHeight; y += GRID_SPACING)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = canvasWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(GRID_COLOR),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
                gridElements.Add(line);
            }
        }

        /// <summary>
        /// Adds coordinate markers to the canvas
        /// </summary>
        public static void AddCoordinateMarkers(Canvas canvas, List<UIElement> gridElements)
        {
            if (canvas == null) return;

            var centerX = canvas.ActualWidth > 0 ? canvas.ActualWidth / 2 : 500;
            var centerY = canvas.ActualHeight > 0 ? canvas.ActualHeight / 2 : 300;

            // Center marker
            var centerMarker = new Ellipse
            {
                Width = CENTER_MARKER_SIZE,
                Height = CENTER_MARKER_SIZE,
                Fill = new SolidColorBrush(CENTER_MARKER_COLOR),
                Stroke = new SolidColorBrush(CENTER_MARKER_STROKE),
                StrokeThickness = 2
            };
            Canvas.SetLeft(centerMarker, centerX - CENTER_MARKER_SIZE / 2);
            Canvas.SetTop(centerMarker, centerY - CENTER_MARKER_SIZE / 2);
            canvas.Children.Add(centerMarker);
            gridElements.Add(centerMarker);

            // Center coordinates label
            var coordLabel = new TextBlock
            {
                Text = "37.7749, -122.4194",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 10,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
            };
            Canvas.SetLeft(coordLabel, centerX + 10);
            Canvas.SetTop(coordLabel, centerY - 10);
            canvas.Children.Add(coordLabel);
            gridElements.Add(coordLabel);
        }

        /// <summary>
        /// Creates a unified drone indicator
        /// </summary>
        public static Border CreateUnifiedDroneIndicator(DronePosition drone, double x, double y)
        {
            var droneIndicator = new Border
            {
                Background = new SolidColorBrush(DRONE_BACKGROUND),
                CornerRadius = new CornerRadius(15),
                Width = DRONE_SIZE,
                Height = DRONE_SIZE,
                BorderBrush = new SolidColorBrush(DRONE_BORDER),
                BorderThickness = new Thickness(2),
                ToolTip = CreateDroneToolTip(drone)
            };

            // Drone icon
            var droneIcon = new TextBlock
            {
                Text = "🚁",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(DRONE_ICON_COLOR)
            };

            droneIndicator.Child = droneIcon;

            // Position the drone
            Canvas.SetLeft(droneIndicator, x - DRONE_SIZE / 2);
            Canvas.SetTop(droneIndicator, y - DRONE_SIZE / 2);

            return droneIndicator;
        }

        /// <summary>
        /// Converts GPS coordinates to canvas coordinates using unified mapping
        /// </summary>
        public static Point ConvertGpsToCanvas(double latitude, double longitude, double canvasWidth, double canvasHeight)
        {
            // Center coordinates around San Francisco (37.7749, -122.4194)
            var centerLat = 37.7749;
            var centerLon = -122.4194;
            
            // Scale factor for better distribution across canvas
            var scaleX = canvasWidth / 0.02;  // 0.02 degrees longitude range
            var scaleY = canvasHeight / 0.02; // 0.02 degrees latitude range
            
            var x = canvasWidth / 2 + (longitude - centerLon) * scaleX;
            var y = canvasHeight / 2 - (latitude - centerLat) * scaleY;

            // Ensure within canvas bounds with proper margin
            x = Math.Max(DRONE_SIZE / 2, Math.Min(canvasWidth - DRONE_SIZE / 2, x));
            y = Math.Max(DRONE_SIZE / 2, Math.Min(canvasHeight - DRONE_SIZE / 2, y));

            return new Point(x, y);
        }

        /// <summary>
        /// Creates a tooltip for the drone
        /// </summary>
        private static ToolTip CreateDroneToolTip(DronePosition drone)
        {
            var toolTip = new ToolTip
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(Colors.White)
            };

            var content = new StackPanel { Margin = new Thickness(5) };
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"ID: {drone.Id}", 
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 2)
            });
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"Name: {drone.Name}",
                Margin = new Thickness(0, 0, 0, 2)
            });
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"Position: {drone.Latitude:F4}, {drone.Longitude:F4}",
                Margin = new Thickness(0, 0, 0, 2)
            });
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"Altitude: {drone.Altitude:F1}m",
                Margin = new Thickness(0, 0, 0, 2)
            });
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"Status: {drone.Status}",
                Margin = new Thickness(0, 0, 0, 2)
            });
            
            content.Children.Add(new TextBlock 
            { 
                Text = $"Battery: {drone.BatteryLevel}%",
                Margin = new Thickness(0, 0, 0, 2)
            });

            toolTip.Content = content;
            return toolTip;
        }

        /// <summary>
        /// Gets the status color for a drone
        /// </summary>
        public static Brush GetStatusColor(DroneFlightStatus status)
        {
            return status switch
            {
                DroneFlightStatus.Flying => new SolidColorBrush(Colors.Green),
                DroneFlightStatus.Hovering => new SolidColorBrush(Colors.Yellow),
                DroneFlightStatus.Landing => new SolidColorBrush(Colors.Orange),
                DroneFlightStatus.Emergency => new SolidColorBrush(Colors.Red),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
    }
}
