using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class DroneTrackingControl : UserControl, INotifyPropertyChanged
    {
        private readonly DroneTrackingService _trackingService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly List<UIElement> _droneIndicators;
        private readonly List<UIElement> _gridLines;
        private readonly Random _random = new Random();

        // Binding properties
        private int _activeDronesCount = 0;
        private double _totalDistance = 0.0;
        private double _averageSpeed = 0.0;
        private string _trackingStatus = "Initializing";
        private string _lastUpdateTime = "Never";

        public event PropertyChangedEventHandler? PropertyChanged;

        public int ActiveDronesCount
        {
            get => _activeDronesCount;
            set { _activeDronesCount = value; OnPropertyChanged(); }
        }

        public double TotalDistance
        {
            get => _totalDistance;
            set { _totalDistance = value; OnPropertyChanged(); }
        }

        public double AverageSpeed
        {
            get => _averageSpeed;
            set { _averageSpeed = value; OnPropertyChanged(); }
        }

        public string TrackingStatus
        {
            get => _trackingStatus;
            set { _trackingStatus = value; OnPropertyChanged(); }
        }

        public string LastUpdateTime
        {
            get => _lastUpdateTime;
            set { _lastUpdateTime = value; OnPropertyChanged(); }
        }

        public DroneTrackingControl()
        {
            try
            {
                InitializeComponent();
                DataContext = this;

                _droneIndicators = new List<UIElement>();
                _gridLines = new List<UIElement>();

                // Initialize the tracking service
                _trackingService = new DroneTrackingService();

                // Setup event handlers
                _trackingService.DronePositionUpdated += OnDronePositionUpdated;
                _trackingService.TrackingAlert += OnTrackingAlert;

                // Setup UI update timer
                _uiUpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(2)
                };
                _uiUpdateTimer.Tick += UpdateUI;

                // Wait for the control to load before initializing
                Loaded += OnLoaded;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing DroneTrackingControl: {ex.Message}");
                TrackingStatus = "Error";
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize map after control is loaded
                await InitializeMapAsync();
                
                // Add some demo drones for testing
                await InitializeDemoDronesAsync();
                
                // Start tracking
                _trackingService.StartTracking();
                TrackingStatus = "Active";
                
                // Start UI updates
                _uiUpdateTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in OnLoaded: {ex.Message}");
                TrackingStatus = "Error";
            }
        }

        private async System.Threading.Tasks.Task InitializeMapAsync()
        {
            // Wait for canvas to have actual size
            await Dispatcher.InvokeAsync(() => {
                if (MapCanvas.ActualWidth <= 0 || MapCanvas.ActualHeight <= 0)
                {
                    // Set minimum size if not available
                    MapCanvas.Width = 600;
                    MapCanvas.Height = 300;
                }
                DrawMapGrid();
                AddCoordinateMarkers();
            });
        }

        private async System.Threading.Tasks.Task InitializeDemoDronesAsync()
        {
            try
            {
                // Add some demo drones at different positions
                var demoDrones = new[]
                {
                    new DronePosition 
                    { 
                        Id = "DRONE-001", 
                        Name = "Surveillance Alpha", 
                        Latitude = 37.7749, 
                        Longitude = -122.4194, 
                        Altitude = 50, 
                        Status = DroneFlightStatus.Flying,
                        BatteryLevel = 85,
                        SignalStrength = 92
                    },
                    new DronePosition 
                    { 
                        Id = "DRONE-002", 
                        Name = "Surveillance Beta", 
                        Latitude = 37.7751, 
                        Longitude = -122.4196, 
                        Altitude = 45, 
                        Status = DroneFlightStatus.Hovering,
                        BatteryLevel = 72,
                        SignalStrength = 88
                    },
                    new DronePosition 
                    { 
                        Id = "DRONE-003", 
                        Name = "Surveillance Gamma", 
                        Latitude = 37.7747, 
                        Longitude = -122.4192, 
                        Altitude = 55, 
                        Status = DroneFlightStatus.Flying,
                        BatteryLevel = 91,
                        SignalStrength = 95
                    }
                };

                foreach (var drone in demoDrones)
                {
                    await _trackingService.AddDroneToTrackingAsync(drone);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing demo drones: {ex.Message}");
            }
        }

        private void DrawMapGrid()
        {
            // Clear existing grid lines
            foreach (var line in _gridLines.ToList())
            {
                MapCanvas.Children.Remove(line);
            }
            _gridLines.Clear();

            var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 600;
            var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 300;

            // Vertical grid lines
            for (int x = 0; x < canvasWidth; x += 40)
            {
                var line = new Line
                {
                    X1 = x,
                    Y1 = 0,
                    X2 = x,
                    Y2 = canvasHeight,
                    Stroke = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    StrokeThickness = 1
                };
                MapCanvas.Children.Add(line);
                _gridLines.Add(line);
            }

            // Horizontal grid lines
            for (int y = 0; y < canvasHeight; y += 40)
            {
                var line = new Line
                {
                    X1 = 0,
                    Y1 = y,
                    X2 = canvasWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                    StrokeThickness = 1
                };
                MapCanvas.Children.Add(line);
                _gridLines.Add(line);
            }
        }

        private void AddCoordinateMarkers()
        {
            var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 600;
            var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 300;
            var centerX = canvasWidth / 2;
            var centerY = canvasHeight / 2;

            // Center marker
            var centerMarker = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Colors.Red),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(centerMarker, centerX - 3);
            Canvas.SetTop(centerMarker, centerY - 3);
            MapCanvas.Children.Add(centerMarker);
            _gridLines.Add(centerMarker);

            // Center coordinates label
            var coordLabel = new TextBlock
            {
                Text = "37.7749, -122.4194",
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 8,
                Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
            };
            Canvas.SetLeft(coordLabel, centerX + 8);
            Canvas.SetTop(coordLabel, centerY - 8);
            MapCanvas.Children.Add(coordLabel);
            _gridLines.Add(coordLabel);
        }

        private void UpdateMapDrones()
        {
            try
            {
                // Remove existing drone indicators
                foreach (var indicator in _droneIndicators.ToList())
                {
                    MapCanvas.Children.Remove(indicator);
                }
                _droneIndicators.Clear();

                var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 600;
                var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 300;

                // Add current drone positions
                foreach (var drone in _trackingService.ActiveDronePositions)
                {
                    // Convert GPS coordinates to canvas coordinates (improved mapping)
                    // Center coordinates around San Francisco (37.7749, -122.4194)
                    var centerLat = 37.7749;
                    var centerLon = -122.4194;
                    
                    // Scale factor for better distribution across canvas
                    var scaleX = canvasWidth / 0.02;  // 0.02 degrees longitude range
                    var scaleY = canvasHeight / 0.02; // 0.02 degrees latitude range
                    
                    var x = canvasWidth / 2 + (drone.Longitude - centerLon) * scaleX;
                    var y = canvasHeight / 2 - (drone.Latitude - centerLat) * scaleY;

                    // Ensure within canvas bounds with proper margin
                    x = Math.Max(24, Math.Min(canvasWidth - 24, x));
                    y = Math.Max(24, Math.Min(canvasHeight - 24, y));

                    var droneIndicator = new Border
                    {
                        Background = GetStatusColor(drone.Status),
                        CornerRadius = new CornerRadius(12),
                        Width = 24,
                        Height = 24,
                        BorderBrush = new SolidColorBrush(Colors.White),
                        BorderThickness = new Thickness(2),
                        Child = new TextBlock
                        {
                            Text = "🚁",
                            FontSize = 12,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = new SolidColorBrush(Colors.White)
                        }
                    };

                    Canvas.SetLeft(droneIndicator, x - 12);
                    Canvas.SetTop(droneIndicator, y - 12);
                    
                    MapCanvas.Children.Add(droneIndicator);
                    _droneIndicators.Add(droneIndicator);

                    // Add drone label
                    var droneLabel = new TextBlock
                    {
                        Text = drone.Id,
                        Foreground = new SolidColorBrush(Colors.White),
                        FontSize = 8,
                        Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                    };
                    Canvas.SetLeft(droneLabel, x + 15);
                    Canvas.SetTop(droneLabel, y - 15);
                    
                    MapCanvas.Children.Add(droneLabel);
                    _droneIndicators.Add(droneLabel);

                    // Add movement trail
                    if (_random.NextDouble() < 0.3) // 30% chance to show trail
                    {
                        var trail = new Ellipse
                        {
                            Width = 4,
                            Height = 4,
                            Fill = new SolidColorBrush(Color.FromArgb(100, 0, 120, 212)),
                            Margin = new Thickness(2)
                        };
                        Canvas.SetLeft(trail, x - _random.Next(-20, 20) - 2);
                        Canvas.SetTop(trail, y - _random.Next(-20, 20) - 2);
                        
                        MapCanvas.Children.Add(trail);
                        _droneIndicators.Add(trail);
                    }
                }

                // Update metrics
                ActiveDronesCount = _trackingService.ActiveDronePositions.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating map drones: {ex.Message}");
            }
        }

        private SolidColorBrush GetStatusColor(DroneFlightStatus status)
        {
            return status switch
            {
                DroneFlightStatus.Flying => new SolidColorBrush(Colors.Green),
                DroneFlightStatus.Hovering => new SolidColorBrush(Colors.Orange),
                DroneFlightStatus.Returning => new SolidColorBrush(Colors.Blue),
                DroneFlightStatus.Emergency => new SolidColorBrush(Colors.Red),
                DroneFlightStatus.Grounded => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Yellow)
            };
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            try
            {
                // Update drone positions on map
                UpdateMapDrones();
                
                // Update statistics
                UpdateStatistics();
                
                // Update last update time
                LastUpdateTime = DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in UpdateUI: {ex.Message}");
            }
        }

        private void UpdateStatistics()
        {
            try
            {
                var activeDrones = _trackingService.ActiveDronePositions;
                
                // Calculate total distance traveled (simplified)
                TotalDistance = activeDrones.Sum(d => d.Altitude * 2.5 + _random.NextDouble() * 100);
                
                // Calculate average speed
                AverageSpeed = activeDrones.Count > 0 ? 
                    activeDrones.Average(d => 10 + _random.NextDouble() * 20) : 0;
                
                // Simulate drone movement for demo purposes
                foreach (var drone in activeDrones.ToList())
                {
                    // Randomly move drones slightly
                    if (_random.NextDouble() < 0.4) // 40% chance of movement
                    {
                        drone.Latitude += (_random.NextDouble() - 0.5) * 0.0005;
                        drone.Longitude += (_random.NextDouble() - 0.5) * 0.0005;
                        drone.Altitude += (_random.NextDouble() - 0.5) * 2;
                        
                        // Clamp altitude
                        drone.Altitude = Math.Max(20, Math.Min(100, drone.Altitude));
                        
                        // Update battery level slightly
                        drone.BatteryLevel -= _random.NextDouble() * 0.1;
                        drone.BatteryLevel = Math.Max(10, drone.BatteryLevel);
                        
                        // Update signal strength
                        drone.SignalStrength += (_random.NextDouble() - 0.5) * 5;
                        drone.SignalStrength = Math.Max(20, Math.Min(100, drone.SignalStrength));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating statistics: {ex.Message}");
            }
        }

        private void OnDronePositionUpdated(object? sender, DroneTrackingEventArgs e)
        {
            // Will be handled by the UpdateUI timer
        }

        private void OnTrackingAlert(object? sender, string alert)
        {
            // Could add visual alerts to the map if needed
            Console.WriteLine($"Tracking Alert: {alert}");
        }

        public void StartTracking()
        {
            try
            {
                _trackingService?.StartTracking();
                _uiUpdateTimer?.Start();
                TrackingStatus = "Active";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting tracking: {ex.Message}");
                TrackingStatus = "Error";
            }
        }

        public void StopTracking()
        {
            try
            {
                _trackingService?.StopTracking();
                _uiUpdateTimer?.Stop();
                TrackingStatus = "Stopped";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping tracking: {ex.Message}");
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            
            // Redraw grid when size changes
            if (IsLoaded)
            {
                DrawMapGrid();
                AddCoordinateMarkers();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Cleanup
        public void Dispose()
        {
            try
            {
                _uiUpdateTimer?.Stop();
                _trackingService?.StopTracking();
                _trackingService?.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing DroneTrackingControl: {ex.Message}");
            }
        }
    }
}
