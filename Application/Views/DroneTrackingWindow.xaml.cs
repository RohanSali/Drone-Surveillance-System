using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DroneSurveillanceSystem.Services;
using System.Linq;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DroneSurveillanceSystem.Views
{
    public partial class DroneTrackingWindow : UserControl, INotifyPropertyChanged
    {
        private readonly DroneTrackingService _trackingService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly DispatcherTimer _sessionTimer;
        private readonly List<UIElement> _droneIndicators;
        private readonly List<Border> _alertElements;
        private DateTime _sessionStartTime;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? CloseRequested;

        public DroneTrackingWindow()
        {
            InitializeComponent();
            DataContext = this;

            _trackingService = DroneTrackingService.Instance;
            _droneIndicators = new List<UIElement>();
            _alertElements = new List<Border>();
            _sessionStartTime = DateTime.Now;

            // Setup event handlers
            _trackingService.DronePositionUpdated += OnDronePositionUpdated;
            // _trackingService.TrackingAlert += OnTrackingAlert;
            _trackingService.PropertyChanged += OnTrackingServicePropertyChanged;

            // Setup UI update timer
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();

            // Setup session timer
            _sessionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _sessionTimer.Tick += UpdateSessionTime;
            _sessionTimer.Start();

            // Initialize map
            InitializeMap();
            
            // Clear sample elements and load real data
            LoadRealDroneData();
        }

        private void InitializeMap()
        {
            // Draw grid on map canvas
            DrawMapGrid();
            
            // Add coordinate markers
            AddCoordinateMarkers();
        }

        private void DrawMapGrid()
        {
            MapCanvas.Children.Clear();
            UnifiedGridService.DrawUnifiedGrid(MapCanvas, new List<UIElement>());
        }

        private void AddCoordinateMarkers()
        {
            UnifiedGridService.AddCoordinateMarkers(MapCanvas, new List<UIElement>());
        }

        private void LoadRealDroneData()
        {
            // Clear sample drone status panel
            DroneStatusPanel.Children.Clear();
            
            // Update with real drone data
            UpdateDroneStatusCards();
            UpdateMapDrones();
        }

        private void UpdateDroneStatusCards()
        {
            DroneStatusPanel.Children.Clear();

            foreach (var drone in _trackingService.ActiveDronePositions)
            {
                var droneCard = CreateDroneStatusCard(drone);
                DroneStatusPanel.Children.Add(droneCard);
            }
        }

        private Border CreateDroneStatusCard(DronePosition drone)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(5)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var statusIndicator = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = GetStatusColor(drone.Status),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var droneNameText = new TextBlock
            {
                Text = drone.Name,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                FontSize = 14
            };

            headerPanel.Children.Add(statusIndicator);
            headerPanel.Children.Add(droneNameText);

            var statusText = new TextBlock
            {
                Text = drone.StatusText.ToUpper(),
                Foreground = GetStatusColor(drone.Status),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };

            Grid.SetColumn(headerPanel, 0);
            Grid.SetColumn(statusText, 1);
            headerGrid.Children.Add(headerPanel);
            headerGrid.Children.Add(statusText);

            // Data Grid
            var dataGrid = new Grid();
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dataGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftPanel = new StackPanel();
            leftPanel.Children.Add(new TextBlock { Text = "Position:", Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), FontSize = 12, Margin = new Thickness(5, 2, 5, 2) });
            leftPanel.Children.Add(new TextBlock { Text = drone.CoordinatesText, Foreground = new SolidColorBrush(Colors.White), FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 2, 5, 2) });
            leftPanel.Children.Add(new TextBlock { Text = "Altitude:", Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), FontSize = 12, Margin = new Thickness(5, 7, 5, 2) });
            leftPanel.Children.Add(new TextBlock { Text = drone.AltitudeText, Foreground = new SolidColorBrush(Colors.White), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 2, 5, 2) });

            var rightPanel = new StackPanel();
            rightPanel.Children.Add(new TextBlock { Text = "Battery:", Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), FontSize = 12, Margin = new Thickness(5, 2, 5, 2) });
            var batteryBar = new ProgressBar
            {
                Value = drone.BatteryLevel,
                Maximum = 100,
                Height = 15,
                Foreground = drone.BatteryLevel > 50 ? new SolidColorBrush(Colors.Green) : 
                           drone.BatteryLevel > 20 ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.Red),
                Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            rightPanel.Children.Add(batteryBar);
            rightPanel.Children.Add(new TextBlock { Text = "Signal:", Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)), FontSize = 12, Margin = new Thickness(5, 7, 5, 2) });
            var signalBar = new ProgressBar
            {
                Value = drone.SignalStrength,
                Maximum = 100,
                Height = 15,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Background = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            rightPanel.Children.Add(signalBar);

            Grid.SetColumn(leftPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            dataGrid.Children.Add(leftPanel);
            dataGrid.Children.Add(rightPanel);

            // Status Bar
            var statusBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 10, 0, 0)
            };

            var statusBarGrid = new Grid();
            statusBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusBarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var statusInfo = new TextBlock
            {
                Text = $"{drone.StatusText} - Speed: {drone.SpeedText}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 11
            };

            var lastSeen = new TextBlock
            {
                Text = $"{(DateTime.Now - drone.LastSeen).TotalSeconds:F0}s ago",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 10
            };

            Grid.SetColumn(statusInfo, 0);
            Grid.SetColumn(lastSeen, 1);
            statusBarGrid.Children.Add(statusInfo);
            statusBarGrid.Children.Add(lastSeen);
            statusBar.Child = statusBarGrid;

            Grid.SetRow(headerGrid, 0);
            Grid.SetRow(dataGrid, 1);
            Grid.SetRow(statusBar, 2);
            grid.Children.Add(headerGrid);
            grid.Children.Add(dataGrid);
            grid.Children.Add(statusBar);

            card.Child = grid;
            return card;
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

        private void UpdateMapDrones()
        {
            // Remove existing drone indicators
            foreach (var indicator in _droneIndicators.ToList())
            {
                MapCanvas.Children.Remove(indicator);
            }
            _droneIndicators.Clear();

            // Add current drone positions
            var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 1000;
            var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

            foreach (var drone in _trackingService.ActiveDronePositions)
            {
                // Convert GPS coordinates to canvas coordinates using unified service
                var point = UnifiedGridService.ConvertGpsToCanvas(drone.Latitude, drone.Longitude, canvasWidth, canvasHeight);
                
                // Create unified drone indicator
                var droneIndicator = UnifiedGridService.CreateUnifiedDroneIndicator(drone, point.X, point.Y);
                
                MapCanvas.Children.Add(droneIndicator);
                _droneIndicators.Add(droneIndicator);
            }
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            // Update drone status cards
            UpdateDroneStatusCards();
            
            // Update map
            UpdateMapDrones();
        }

        private void UpdateSessionTime(object? sender, EventArgs e)
        {
            var sessionDuration = DateTime.Now - _sessionStartTime;
            SessionTimeText.Text = sessionDuration.ToString(@"hh\:mm\:ss");
        }

        private void OnDronePositionUpdated(object? sender, DroneTrackingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                // Update UI will be called by timer
            });
        }

        private void OnTrackingServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Properties are bound directly, no additional action needed
        }

        // Event Handlers
        private void StartTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            _trackingService.StartTracking();
        }

        private void StopTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            _trackingService.StopTracking();
        }

        private void AddDroneButton_Click(object sender, RoutedEventArgs e)
        {
            // Create a new sample drone
            var newDrone = new DronePosition
            {
                Id = $"DRONE-{_trackingService.ActiveDronePositions.Count + 1:000}",
                Name = $"Surveillance {(char)('A' + _trackingService.ActiveDronePositions.Count)}",
                Latitude = 37.7749 + (new Random().NextDouble() - 0.5) * 0.01,
                Longitude = -122.4194 + (new Random().NextDouble() - 0.5) * 0.01,
                Altitude = 40 + new Random().NextDouble() * 20,
                Status = DroneFlightStatus.Flying,
                BatteryLevel = 80 + new Random().NextDouble() * 20,
                SignalStrength = 70 + new Random().NextDouble() * 30
            };

            _ = _trackingService.AddDroneToTrackingAsync(newDrone);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Cleanup()
        {
            _uiUpdateTimer?.Stop();
            _sessionTimer?.Stop();
            _trackingService?.Dispose();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}