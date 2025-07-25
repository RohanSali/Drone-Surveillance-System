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
    public partial class DroneTrackingWindow : Window, INotifyPropertyChanged
    {
        private readonly DroneTrackingService _trackingService;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly DispatcherTimer _sessionTimer;
        private readonly List<UIElement> _droneIndicators;
        private readonly List<Border> _alertElements;
        private DateTime _sessionStartTime;
        private int _dataPointsCounter = 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public DroneTrackingWindow()
        {
            InitializeComponent();
            DataContext = this;

            _trackingService = new DroneTrackingService();
            _droneIndicators = new List<UIElement>();
            _alertElements = new List<Border>();
            _sessionStartTime = DateTime.Now;

            // Setup event handlers
            _trackingService.DronePositionUpdated += OnDronePositionUpdated;
            _trackingService.TrackingAlert += OnTrackingAlert;
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
            
            var canvasWidth = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth : 1000;
            var canvasHeight = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight : 600;

            // Vertical grid lines
            for (int x = 0; x < canvasWidth; x += 50)
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
            }

            // Horizontal grid lines
            for (int y = 0; y < canvasHeight; y += 50)
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
            }
        }

        private void AddCoordinateMarkers()
        {
            // Add coordinate reference points
            var centerX = MapCanvas.ActualWidth > 0 ? MapCanvas.ActualWidth / 2 : 500;
            var centerY = MapCanvas.ActualHeight > 0 ? MapCanvas.ActualHeight / 2 : 300;

            // Center marker
            var centerMarker = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Colors.Red),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetLeft(centerMarker, centerX - 4);
            Canvas.SetTop(centerMarker, centerY - 4);
            MapCanvas.Children.Add(centerMarker);

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
            MapCanvas.Children.Add(coordLabel);
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
                // Convert GPS coordinates to canvas coordinates (simplified mapping)
                var x = (drone.Longitude + 122.4194) * 10000 + canvasWidth / 2;
                var y = canvasHeight / 2 - (drone.Latitude - 37.7749) * 10000;

                // Ensure within canvas bounds
                x = Math.Max(15, Math.Min(canvasWidth - 15, x));
                y = Math.Max(15, Math.Min(canvasHeight - 15, y));

                var droneIndicator = new Border
                {
                    Background = GetStatusColor(drone.Status),
                    CornerRadius = new CornerRadius(15),
                    Width = 30,
                    Height = 30,
                    BorderBrush = new SolidColorBrush(Colors.White),
                    BorderThickness = new Thickness(2),
                    Child = new TextBlock
                    {
                        Text = "🚁",
                        FontSize = 16,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };

                Canvas.SetLeft(droneIndicator, x - 15);
                Canvas.SetTop(droneIndicator, y - 15);
                
                MapCanvas.Children.Add(droneIndicator);
                _droneIndicators.Add(droneIndicator);

                // Add drone label
                var droneLabel = new TextBlock
                {
                    Text = drone.Id,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 10,
                    Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0))
                };
                Canvas.SetLeft(droneLabel, x + 20);
                Canvas.SetTop(droneLabel, y - 20);
                
                MapCanvas.Children.Add(droneLabel);
                _droneIndicators.Add(droneLabel);
            }
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            _dataPointsCounter++;
            DataPointsText.Text = _dataPointsCounter.ToString("N0");
            
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

        private void OnTrackingAlert(object? sender, string alert)
        {
            Dispatcher.Invoke(() =>
            {
                AddAlert(alert);
            });
        }

        private void AddAlert(string alertMessage)
        {
            var alertBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 26)),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 5, 0, 5)
            };

            var alertGrid = new Grid();
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            alertGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var indicator = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(Colors.Red),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var messageText = new TextBlock
            {
                Text = alertMessage,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 2, 5, 2)
            };

            var timeText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(indicator, 0);
            Grid.SetColumn(messageText, 1);
            Grid.SetColumn(timeText, 2);

            alertGrid.Children.Add(indicator);
            alertGrid.Children.Add(messageText);
            alertGrid.Children.Add(timeText);

            alertBorder.Child = alertGrid;

            AlertsPanel.Children.Insert(0, alertBorder);
            _alertElements.Add(alertBorder);

            // Keep only last 10 alerts
            while (AlertsPanel.Children.Count > 10)
            {
                AlertsPanel.Children.RemoveAt(AlertsPanel.Children.Count - 1);
            }

            // Update alert count
            AlertCountText.Text = AlertsPanel.Children.Count.ToString();
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
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _uiUpdateTimer?.Stop();
            _sessionTimer?.Stop();
            _trackingService?.Dispose();
            base.OnClosed(e);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
