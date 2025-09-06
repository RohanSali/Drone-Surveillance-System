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
                _trackingService = DroneTrackingService.Instance;

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

        private void DrawMapGrid()
        {
            UnifiedGridService.DrawUnifiedGrid(MapCanvas, _gridLines);
        }

        private void AddCoordinateMarkers()
        {
            UnifiedGridService.AddCoordinateMarkers(MapCanvas, _gridLines);
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
                    // Convert GPS coordinates to canvas coordinates using unified service
                    var point = UnifiedGridService.ConvertGpsToCanvas(drone.Latitude, drone.Longitude, canvasWidth, canvasHeight);
                    
                    // Create unified drone indicator
                    var droneIndicator = UnifiedGridService.CreateUnifiedDroneIndicator(drone, point.X, point.Y);
                    
                    MapCanvas.Children.Add(droneIndicator);
                    _droneIndicators.Add(droneIndicator);
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
                
                // Calculate total distance traveled (with fallback for missing DistanceTraveled property)
                TotalDistance = activeDrones.Sum(d => 
                {
                    // Use reflection to check if DistanceTraveled property exists
                    var property = d.GetType().GetProperty("DistanceTraveled");
                    if (property != null && property.PropertyType == typeof(double))
                    {
                        return (double)property.GetValue(d);
                    }
                    return 0.0; // Default value if property doesn't exist
                });
                
                // Calculate average speed (with fallback for missing Speed property)
                AverageSpeed = activeDrones.Count > 0 ? 
                    activeDrones.Average(d => 
                    {
                        // Use reflection to check if Speed property exists
                        var property = d.GetType().GetProperty("Speed");
                        if (property != null && property.PropertyType == typeof(double))
                        {
                            return (double)property.GetValue(d);
                        }
                        return 0.0; // Default value if property doesn't exist
                    }) : 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating statistics: {ex.Message}");
                // Set default values to avoid UI errors
                TotalDistance = 0;
                AverageSpeed = 0;
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