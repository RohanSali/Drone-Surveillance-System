using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;
using Microsoft.Win32;

namespace DroneSurveillanceSystem.Views
{
    public partial class NetworkDetailsPage : Window
    {
        private readonly Network _network;
        private readonly NetworkService _networkService;
        private readonly DispatcherTimer _updateTimer;
        private readonly Random _random = new Random();
        private readonly LostFindingManager _lostFindingManager;
        private List<NetworkDrone> _connectedDrones;
        private List<NetworkCctv> _connectedCctvs;
        private List<NetworkAlert> _activeAlerts;

        public NetworkDetailsPage(Network network)
        {
            InitializeComponent();
            
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _networkService = new NetworkService();
            
            // Initialize collections
            _connectedDrones = new List<NetworkDrone>();
            _connectedCctvs = new List<NetworkCctv>();
            _activeAlerts = new List<NetworkAlert>();
            
            // Initialize the Lost Finding manager
            _lostFindingManager = LostFindingManager.Instance;
            
            // Setup UI
            InitializeUI();
            
            // Generate data based on actual network
            GenerateNetworkData();
            
            // Populate UI with data
            PopulateDrones();
            PopulateCctvs();
            PopulateAlerts();
            UpdateNetworkSummary();
            
            // Subscribe to Lost Finding manager updates
            _lostFindingManager.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_lostFindingManager.PendingCount))
                {
                    Dispatcher.Invoke(() => UpdatePendingButton());
                }
            };
            
            // Subscribe to AlertManager changes for real-time updates
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    PopulateAlerts();
                    UpdateNetworkSummary();
                });
            };
            
            // Setup timer for real-time updates
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(2);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void InitializeUI()
        {
            NetworkNameText.Text = _network.Name ?? "Unknown Network";
            NetworkDescriptionText.Text = _network.Description ?? "No description available";
            
            // Set network status and icon color based on network name
            switch (_network.Name ?? "Unknown")
            {
                case "Network 1":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    NetworkStatusText.Text = "Active";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")); // Green
                    break;
                case "Network 2":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    NetworkStatusText.Text = "Active";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2196F3")); // Blue
                    break;
                case "Network 3":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                    NetworkStatusText.Text = "Standby";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); // Orange
                    break;
                case "Network 4":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    NetworkStatusText.Text = "Offline";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.Red);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")); // Red
                    break;
                case "Network 5":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.Purple);
                    NetworkStatusText.Text = "Testing";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.Purple);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")); // Purple
                    break;
                case "Network 6":
                    NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
                    NetworkStatusText.Text = "Active";
                    NetworkStatusText.Foreground = new SolidColorBrush(Colors.LimeGreen);
                    NetworkIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00BCD4")); // Cyan
                    break;
            }
        }

        private void GenerateNetworkData()
        {
            // Generate drones based on actual network assignments
            int droneIndex = 1;
            if (_network.Drones != null)
            {
                foreach (var assignedDrone in _network.Drones)
                {
                    var drone = new NetworkDrone
                    {
                        Id = $"{(_network.Name ?? "Unknown").Replace(" ", "")}-D{droneIndex:D3}",
                        Name = assignedDrone.Name ?? "Unknown Drone",
                        Status = GetRandomDroneStatus(),
                        Battery = 60 + _random.Next(35), // 60-95%
                        Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.02,
                        Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.02,
                        Altitude = 30 + _random.Next(40), // 30-70m
                        LastSeen = DateTime.Now.AddMinutes(-_random.Next(10))
                    };
                    
                    _connectedDrones.Add(drone);
                    droneIndex++;
                }
            }

            // If no drones are assigned, show a message
            if (_connectedDrones.Count == 0)
            {
                // Add a placeholder drone to show "No drones assigned"
                var placeholderDrone = new NetworkDrone
                {
                    Id = "NO-DRONES",
                    Name = "No drones assigned to this network",
                    Status = "Inactive",
                    Battery = 0,
                    Latitude = 0,
                    Longitude = 0,
                    Altitude = 0,
                    LastSeen = DateTime.Now
                };
                _connectedDrones.Add(placeholderDrone);
            }

            // Generate CCTVs based on actual network assignments
            int camIndex = 1;
            if (_network.Cctvs != null)
            {
                foreach (var cam in _network.Cctvs)
                {
                    var cctv = new NetworkCctv
                    {
                        Id = $"{(_network.Name ?? "Unknown").Replace(" ", "")}-C{camIndex:D3}",
                        Name = cam.Name ?? "Unknown CCTV",
                        IsOnline = true,
                        Resolution = "1080p",
                        Fps = 30,
                        LastSeen = DateTime.Now.AddMinutes(-_random.Next(10))
                    };
                    _connectedCctvs.Add(cctv);
                    camIndex++;
                }
            }

            // Generate sample alerts based on network
            int alertCount = GetAlertCountForNetwork(_network.Name ?? "Unknown");
            string[] alertTypes = { "Intrusion Detected", "Low Battery Warning", "Communication Lost", "Anomaly Detected", "Crowd Detected" };
            string[] alertSeverities = { "High", "Medium", "Low" };

            for (int i = 0; i < alertCount; i++)
            {
                var alert = new NetworkAlert
                {
                    Id = $"ALERT-{i + 1:D4}",
                    Type = alertTypes[_random.Next(alertTypes.Length)],
                    Severity = alertSeverities[_random.Next(alertSeverities.Length)],
                    DroneId = _connectedDrones.Count > 0 ? _connectedDrones[_random.Next(_connectedDrones.Count)].Id : "NO-DRONES",
                    Message = GenerateAlertMessage(),
                    Timestamp = DateTime.Now.AddMinutes(-_random.Next(30)),
                    IsActive = true
                };
                
                _activeAlerts.Add(alert);
            }
        }

        private string GetRandomDroneStatus()
        {
            string[] statuses = { "Flying", "Hovering", "Patrolling", "Returning", "Charging" };
            return statuses[_random.Next(statuses.Length)];
        }

        private int GetDroneCountForNetwork(string networkName)
        {
            // Get the actual network from the service
            if (_networkService?.Networks == null) return 0;
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == networkName);
            return network?.Drones?.Count ?? 0;
        }

        private int GetAlertCountForNetwork(string networkName)
        {
            // Get the actual network from the service
            if (_networkService?.Networks == null) return 0;
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == networkName);
            if (network == null) return 0;
            
            // Return filtered alerts count for this network
            return AlertManager.Instance.GetAlertsForNetwork(network).Count();
        }

        private string GenerateAlertMessage()
        {
            string[] messages = {
                "Unauthorized movement detected in restricted zone",
                "Drone battery level below 20% threshold",
                "Lost communication with ground control",
                "Unusual activity pattern identified",
                "Large crowd gathering detected",
                "Obstacle detected in flight path",
                "Weather conditions affecting operations"
            };
            
            return messages[_random.Next(messages.Length)];
        }

        private void PopulateDrones()
        {
            DronesPanel.Children.Clear();

            foreach (var drone in _connectedDrones)
            {
                var droneCard = CreateDroneCard(drone);
                DronesPanel.Children.Add(droneCard);
            }
        }

        private void PopulateCctvs()
        {
            CctvsPanel.Children.Clear();

            if (_connectedCctvs.Count == 0)
            {
                var noCctvText = new TextBlock
                {
                    Text = "No CCTVs assigned to this network",
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                CctvsPanel.Children.Add(noCctvText);
                return;
            }

            foreach (var cam in _connectedCctvs)
            {
                var card = CreateCctvCard(cam);
                CctvsPanel.Children.Add(card);
            }
        }

        private Border CreateDroneCard(NetworkDrone drone)
        {
            // Special handling for placeholder drone
            if (drone.Id == "NO-DRONES")
            {
                var placeholderCard = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(20),
                    Margin = new Thickness(0, 0, 0, 10),
                    BorderThickness = new Thickness(1, 1, 1, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100))
                };

                var placeholderContent = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                
                var iconText = new TextBlock
                {
                    Text = "🚁",
                    FontSize = 32,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                placeholderContent.Children.Add(iconText);

                var messageText = new TextBlock
                {
                    Text = drone.Name,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 14,
                    FontWeight = FontWeights.Medium,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                placeholderContent.Children.Add(messageText);

                placeholderCard.Child = placeholderContent;
                return placeholderCard;
            }

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Drone icon
            var droneIcon = new TextBlock
            {
                Text = "🚁",
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 188, 212)), // Cyan
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(droneIcon, 0);
            grid.Children.Add(droneIcon);

            // Left side - Drone info
            var leftPanel = new StackPanel();
            
            var nameText = new TextBlock
            {
                Text = drone.Name,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            };
            leftPanel.Children.Add(nameText);

            var idText = new TextBlock
            {
                Text = $"ID: {drone.Id}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            leftPanel.Children.Add(idText);

            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var statusIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetStatusColor(drone.Status),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var statusText = new TextBlock
            {
                Text = drone.Status,
                Foreground = GetStatusColor(drone.Status),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            statusPanel.Children.Add(statusIndicator);
            statusPanel.Children.Add(statusText);
            leftPanel.Children.Add(statusPanel);

            var positionText = new TextBlock
            {
                Text = $"Position: {drone.Latitude:F4}, {drone.Longitude:F4}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 0, 2)
            };
            leftPanel.Children.Add(positionText);

            var altitudeText = new TextBlock
            {
                Text = $"Altitude: {drone.Altitude}m",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 10
            };
            leftPanel.Children.Add(altitudeText);

            Grid.SetColumn(leftPanel, 1);
            grid.Children.Add(leftPanel);

            // Right side - Battery and last seen
            var rightPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            
            var batteryText = new TextBlock
            {
                Text = $"{drone.Battery}%",
                Foreground = GetBatteryColor(drone.Battery),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };
            rightPanel.Children.Add(batteryText);

            var batteryLabel = new TextBlock
            {
                Text = "Battery",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            rightPanel.Children.Add(batteryLabel);

            var lastSeenText = new TextBlock
            {
                Text = $"Last seen:\n{drone.LastSeen:HH:mm:ss}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            rightPanel.Children.Add(lastSeenText);

            Grid.SetColumn(rightPanel, 2);
            grid.Children.Add(rightPanel);

            card.Child = grid;
            return card;
        }

        private Border CreateCctvCard(NetworkCctv cam)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 68))
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new TextBlock
            {
                Text = "📷",
                FontSize = 24,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = cam.Name,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 5)
            });
            left.Children.Add(new TextBlock
            {
                Text = $"ID: {cam.Id}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            var statusIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = cam.IsOnline ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var statusText = new TextBlock
            {
                Text = cam.IsOnline ? "Online" : "Offline",
                Foreground = cam.IsOnline ? new SolidColorBrush(Colors.LimeGreen) : new SolidColorBrush(Colors.Red),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            statusPanel.Children.Add(statusIndicator);
            statusPanel.Children.Add(statusText);
            left.Children.Add(statusPanel);

            left.Children.Add(new TextBlock
            {
                Text = $"Resolution: {cam.Resolution} @ {cam.Fps} FPS",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 10
            });
            Grid.SetColumn(left, 1);
            grid.Children.Add(left);

            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock
            {
                Text = $"Last seen:\n{cam.LastSeen:HH:mm:ss}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 9,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });
            Grid.SetColumn(right, 2);
            grid.Children.Add(right);

            card.Child = grid;
            return card;
        }

        private void PopulateAlerts()
        {
            AlertsPanel.Children.Clear();

            // Get alerts from AlertManager filtered by this network
            var networkAlerts = AlertManager.Instance.GetAlertsForNetwork(_network).ToList();

            if (networkAlerts.Count == 0)
            {
                var noAlertsText = new TextBlock
                {
                    Text = "No active alerts",
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                AlertsPanel.Children.Add(noAlertsText);
                return;
            }

            foreach (var alert in networkAlerts)
            {
                var alertCard = CreateAlertCardFromAlertData(alert);
                AlertsPanel.Children.Add(alertCard);
            }
        }

        private Border CreateAlertCardFromAlertData(AlertData alert)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 42, 42)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 5, 0, 5),
                BorderBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
                BorderThickness = new Thickness(1, 1, 1, 1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftPanel = new StackPanel();

            var alertText = new TextBlock
            {
                Text = alert.Alert ?? "Unknown Alert",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 165, 165)),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            leftPanel.Children.Add(alertText);

            var droneIdText = new TextBlock
            {
                Text = $"Device: {alert.DroneId ?? "Unknown"}",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftPanel.Children.Add(droneIdText);

            var timestampText = new TextBlock
            {
                Text = $"Time: {alert.Timestamp ?? DateTime.Now.ToString("HH:mm:ss")}",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftPanel.Children.Add(timestampText);

            var scoreText = new TextBlock
            {
                Text = $"Score: {alert.Score:F1}%",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontSize = 10,
                Margin = new Thickness(0, 2, 0, 0)
            };
            leftPanel.Children.Add(scoreText);

            Grid.SetColumn(leftPanel, 0);
            grid.Children.Add(leftPanel);

            var rightPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var statusText = new TextBlock
            {
                Text = "ACTIVE",
                Foreground = new SolidColorBrush(Color.FromRgb(255, 68, 68)),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            rightPanel.Children.Add(statusText);

            Grid.SetColumn(rightPanel, 1);
            grid.Children.Add(rightPanel);

            card.Child = grid;
            return card;
        }

        private Border CreateAlertCard(NetworkAlert alert)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(74, 26, 26)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(1, 1, 1, 1),
                BorderBrush = GetSeverityColor(alert.Severity)
            };

            var panel = new StackPanel();

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 5) };
            
            // Alert icon
            var alertIcon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 16,
                Foreground = GetSeverityColor(alert.Severity),
                Margin = new Thickness(0, 0, 8, 0)
            };
            headerPanel.Children.Add(alertIcon);
            
            var severityIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = GetSeverityColor(alert.Severity),
                Margin = new Thickness(0, 0, 8, 0)
            };
            headerPanel.Children.Add(severityIndicator);

            var typeText = new TextBlock
            {
                Text = alert.Type,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            headerPanel.Children.Add(typeText);

            var severityText = new TextBlock
            {
                Text = $"({alert.Severity})",
                Foreground = GetSeverityColor(alert.Severity),
                FontSize = 12,
                Margin = new Thickness(8, 0, 0, 0)
            };
            headerPanel.Children.Add(severityText);

            panel.Children.Add(headerPanel);

            var messageText = new TextBlock
            {
                Text = alert.Message,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
            panel.Children.Add(messageText);

            var footerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            
            var droneText = new TextBlock
            {
                Text = $"Drone: {alert.DroneId}",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 10,
                Margin = new Thickness(0, 0, 15, 0)
            };
            footerPanel.Children.Add(droneText);

            var timeText = new TextBlock
            {
                Text = alert.Timestamp.ToString("HH:mm:ss"),
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 10
            };
            footerPanel.Children.Add(timeText);

            panel.Children.Add(footerPanel);

            card.Child = panel;
            return card;
        }

        private Brush GetStatusColor(string status)
        {
            return status switch
            {
                "Flying" or "Patrolling" => new SolidColorBrush(Colors.LimeGreen),
                "Hovering" => new SolidColorBrush(Colors.Orange),
                "Returning" => new SolidColorBrush(Colors.Yellow),
                "Charging" => new SolidColorBrush(Colors.CornflowerBlue),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        private Brush GetBatteryColor(int battery)
        {
            return battery switch
            {
                >= 70 => new SolidColorBrush(Colors.LimeGreen),
                >= 30 => new SolidColorBrush(Colors.Orange),
                _ => new SolidColorBrush(Colors.Red)
            };
        }

        private Brush GetSeverityColor(string severity)
        {
            return severity switch
            {
                "High" => new SolidColorBrush(Colors.Red),
                "Medium" => new SolidColorBrush(Colors.Orange),
                "Low" => new SolidColorBrush(Colors.Yellow),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        private void UpdateNetworkSummary()
        {
            // Count actual assigned drones (excluding placeholder)
            int activeDrones = _connectedDrones.Count(d => d.Id != "NO-DRONES");
            int activeCctvs = _connectedCctvs.Count;
            int totalAlerts = AlertManager.Instance.GetAlertsForNetwork(_network).Count();
            double avgBattery = _connectedDrones.Count > 0 && _connectedDrones.Any(d => d.Id != "NO-DRONES") 
                ? _connectedDrones.Where(d => d.Id != "NO-DRONES").Average(d => d.Battery) 
                : 0;
            
            ActiveDronesText.Text = activeDrones.ToString();
            ActiveCctvsText.Text = activeCctvs.ToString();
            TotalCctvsText.Text = _network.Cctvs?.Count.ToString() ?? "0";
            TotalAlertsText.Text = totalAlerts.ToString();
            DroneCountText.Text = $"({activeDrones} Active)";
            AvgBatteryText.Text = $"{avgBattery:F0}%";
            
            // Update pending button count with Lost Finding pending count
            UpdatePendingButton();
            
            // Update coverage area based on network
            CoverageAreaText.Text = (_network.Name ?? "Unknown") switch
            {
                "Network 1" => "15.2 km²",
                "Network 2" => "12.8 km²",
                "Network 3" => "8.5 km²",
                "Network 4" => "5.2 km²",
                _ => "10.0 km²"
            };
        }

        private void UpdatePendingButton()
        {
            // Update the pending button to show Lost Finding pending count instead of alert count
            PendingButton.Content = $"⏳ {_lostFindingManager.PendingCount}";
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Update last updated time
            LastUpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");
            
            // Simulate some minor updates
            if (_random.Next(100) < 20) // 20% chance
            {
                // Update a random drone's position slightly
                if (_connectedDrones.Count > 0)
                {
                    var drone = _connectedDrones[_random.Next(_connectedDrones.Count)];
                    drone.Latitude += (_random.NextDouble() - 0.5) * 0.0001;
                    drone.Longitude += (_random.NextDouble() - 0.5) * 0.0001;
                    drone.LastSeen = DateTime.Now;
                    
                    // Refresh drone display
                    PopulateDrones();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _updateTimer?.Stop();
            this.Close();
        }
        
        private void AcknowledgeAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var networkAlerts = AlertManager.Instance.GetAlertsForNetwork(_network).ToList();
                
                if (networkAlerts.Count == 0)
                {
                    MessageBox.Show("No active alerts to acknowledge.", "Information", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var result = MessageBox.Show($"Are you sure you want to acknowledge all {networkAlerts.Count} active alerts for {_network.Name ?? "Unknown"}?", 
                    "Acknowledge Alerts", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Remove alerts from AlertManager
                    foreach (var alert in networkAlerts)
                    {
                        AlertManager.Instance.ActiveAlerts.Remove(alert);
                    }
                    
                    // Refresh display
                    PopulateAlerts();
                    UpdateNetworkSummary();
                    
                    MessageBox.Show($"All alerts for {_network.Name ?? "Unknown"} have been acknowledged.", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error acknowledging alerts: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void LostFindingButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Prompt user to select an image
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (openFileDialog.ShowDialog() != true)
                return;

            // 2. Read image as byte array and convert to base64
            byte[] imageBytes;
            string base64Image;
            try
            {
                imageBytes = File.ReadAllBytes(openFileDialog.FileName);
                base64Image = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Prepare WebSocket message (type 'alert_image' with required fields)
            string uniqueName = $"LostFinding_{(_network.Name ?? "Unknown").Replace(" ", "")}_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            
            var wsMessage = new
            {
                type = "alert_image",
                data = new {
                    found = 0,
                    name = uniqueName,
                    drone_id = "drone_001",
                    actual_image = base64Image,
                    matched_frame = "", // Send empty string instead of the same image
                    location = new double[] { 0, 0, 0 },
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            };
            string json = System.Text.Json.JsonSerializer.Serialize(wsMessage);

            try
            {
                var apiService = DroneSurveillanceSystem.Services.ApiService.Instance;
                if (apiService == null)
                {
                    MessageBox.Show("WebSocket service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (apiService._client == null)
                {
                    await apiService.StartWebSocketAsync();
                }
                if (apiService._client != null && apiService._client.IsRunning)
                {
                    DroneSurveillanceSystem.Services.LostFindingManager.LogWebSocket("SENT", json);
                    await apiService._client.SendInstant(json);
                    Console.WriteLine($"[NetworkDetailsPage] Sent alert_image request with name: {uniqueName}");
                    MessageBox.Show($"Lost Finding alert image sent for {_network.Name ?? "Unknown"}!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Add to pending requests and store data using persistent manager
                    string requestId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    
                    var lostFindingItem = new LostFindingData
                    {
                        RequestId = requestId,
                        ActualImageBase64 = base64Image,
                        MatchedImageBase64 = "",
                        Location = "",
                        Score = "",
                        Timestamp = DateTime.Now,
                        Name = uniqueName
                    };
                    
                    _lostFindingManager.AddPendingRequest(uniqueName, lostFindingItem);
                    
                    UpdatePendingButton();
                    
                    // Show the Lost Finding section with only the actual image
                    ShowAllLostFindingSections();
                }
                else
                {
                    MessageBox.Show("WebSocket connection failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send WebSocket message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void PendingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show Lost Finding pending requests information
                var pendingInfo = $"Pending Items for {_network.Name ?? "Unknown"}:\n\n" +
                                $"• Drone Status Updates: 0\n" +
                                $"• Lost Finding Requests: {_lostFindingManager.PendingCount}\n" +
                                $"• Alert Confirmations: {_activeAlerts.Count}\n" +
                                $"• System Updates: 0\n\n" +
                                $"Total Pending: {_lostFindingManager.PendingCount}";
                
                if (_lostFindingManager.PendingCount > 0)
                {
                    // Also open the MonitoringAlertsPage to show pending Lost Finding requests
                    var monitoringAlertsPage = new MonitoringAlertsPage();
                    monitoringAlertsPage.Owner = this;
                    monitoringAlertsPage.WindowState = WindowState.Maximized;
                    monitoringAlertsPage.Show();
                }
                
                MessageBox.Show(pendingInfo, $"Pending Items - {_network.Name ?? "Unknown"}", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error retrieving pending items: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
        }

        // Lost Finding functionality methods
        private void ShowAllLostFindingSections()
        {
            Console.WriteLine($"[NetworkDetailsPage] 🔄 ShowAllLostFindingSections called");
            Console.WriteLine($"[NetworkDetailsPage]   - Total data items: {_lostFindingManager.LostFindingData.Count}");
            Console.WriteLine($"[NetworkDetailsPage]   - LostFindingSection visibility: {LostFindingSection.Visibility}");
            
            // Show the main Lost Finding section
            LostFindingSection.Visibility = Visibility.Visible;
            Console.WriteLine($"[NetworkDetailsPage] ✅ Set LostFindingSection visibility to Visible");
            
            // Clear existing content and add all Lost Finding analyses
            ClearLostFindingContent();
            Console.WriteLine($"[NetworkDetailsPage] ✅ Cleared existing content");
            
            // Add each Lost Finding analysis to the scrollable content
            foreach (var data in _lostFindingManager.LostFindingData)
            {
                Console.WriteLine($"[NetworkDetailsPage] 📋 Adding analysis for request: {data.Name}");
                Console.WriteLine($"[NetworkDetailsPage]   - Has matched image: {!string.IsNullOrEmpty(data.MatchedImageBase64)}");
                Console.WriteLine($"[NetworkDetailsPage]   - Matched image length: {data.MatchedImageBase64?.Length ?? 0}");
                AddLostFindingAnalysis(data);
            }
            
            Console.WriteLine($"[NetworkDetailsPage] ✅ Finished adding all analyses");
        }

        private void ClearLostFindingContent()
        {
            // Find the content area and clear it
            LostFindingContentPanel.Children.Clear();
        }

        private void AddLostFindingAnalysis(LostFindingData data)
        {
            // Create a new analysis section
            var analysisSection = CreateLostFindingAnalysisSection(data);
            LostFindingContentPanel.Children.Add(analysisSection);
        }

        private FrameworkElement CreateLostFindingAnalysisSection(LostFindingData data)
        {
            // Create a border for this analysis
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 10, 0, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with timestamp
            var headerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1e1e1e")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = $"🔍 Lost Finding Analysis - {data.Timestamp:HH:mm:ss}",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00d4ff")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var closeButton = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dc3545")),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            closeButton.Click += (s, e) => RemoveLostFindingAnalysis(border);

            headerGrid.Children.Add(headerText);
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 1);

            headerBorder.Child = headerGrid;

            // Content area
            var contentStack = new StackPanel();

            // Image comparison grid
            var imageGrid = new Grid();
            imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (!string.IsNullOrEmpty(data.MatchedImageBase64))
            {
                imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Actual image
            var actualImageBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d2d")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 5, 0)
            };

            var actualImageStack = new StackPanel();
            actualImageStack.Children.Add(new TextBlock
            {
                Text = "📷 Uploaded Image",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00d4ff")),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var actualImage = new Image
            {
                Width = 200,
                Height = 150,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 10)
            };

            try
            {
                var imageBytes = Convert.FromBase64String(data.ActualImageBase64);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(imageBytes);
                bitmap.EndInit();
                actualImage.Source = bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NetworkDetailsPage] Error loading actual image: {ex.Message}");
                actualImage.Source = null;
            }

            actualImageStack.Children.Add(actualImage);
            actualImageBorder.Child = actualImageStack;

            imageGrid.Children.Add(actualImageBorder);
            Grid.SetColumn(actualImageBorder, 0);

            // Matched image (if available)
            if (!string.IsNullOrEmpty(data.MatchedImageBase64))
            {
                var matchedImageBorder = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2d2d2d")),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10),
                    Margin = new Thickness(5, 0, 0, 0)
                };

                var matchedImageStack = new StackPanel();
                matchedImageStack.Children.Add(new TextBlock
                {
                    Text = "✅ Match Found!",
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50")),
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                });

                var matchedImage = new Image
                {
                    Width = 200,
                    Height = 150,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                try
                {
                    var imageBytes = Convert.FromBase64String(data.MatchedImageBase64);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(imageBytes);
                    bitmap.EndInit();
                    matchedImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NetworkDetailsPage] Error loading matched image: {ex.Message}");
                    matchedImage.Source = null;
                }

                matchedImageStack.Children.Add(matchedImage);
                matchedImageBorder.Child = matchedImageStack;

                imageGrid.Children.Add(matchedImageBorder);
                Grid.SetColumn(matchedImageBorder, 1);
            }

            // Info grid
            var infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var locationBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a1a")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 15, 5, 0)
            };

            var locationStack = new StackPanel();
            locationStack.Children.Add(new TextBlock
            {
                Text = "📍 Location",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC107")),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            });
            locationStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(data.Location) ? "—" : data.Location,
                Foreground = Brushes.White,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            });
            locationBorder.Child = locationStack;

            var scoreBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1a1a1a")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(5, 15, 0, 0)
            };

            var scoreStack = new StackPanel();
            scoreStack.Children.Add(new TextBlock
            {
                Text = "🎯 Confidence Score",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9C27B0")),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            });
            scoreStack.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(data.Score) ? "—" : data.Score,
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            });
            scoreBorder.Child = scoreStack;

            // Add info to grid
            infoGrid.Children.Add(locationBorder);
            Grid.SetColumn(locationBorder, 0);
            infoGrid.Children.Add(scoreBorder);
            Grid.SetColumn(scoreBorder, 1);

            // Add all content
            contentStack.Children.Add(imageGrid);
            contentStack.Children.Add(infoGrid);

            grid.Children.Add(headerBorder);
            Grid.SetRow(headerBorder, 0);
            grid.Children.Add(contentStack);
            Grid.SetRow(contentStack, 1);

            border.Child = grid;
            return border;
        }

        private void RemoveLostFindingAnalysis(Border analysisBorder)
        {
            // Remove this analysis from the content panel
            LostFindingContentPanel.Children.Remove(analysisBorder);
            
            // Also remove the data from the manager to update pending count
            // Extract the name from the header text to identify which analysis to remove
            if (analysisBorder.Child is Grid grid && grid.Children.Count > 0)
            {
                if (grid.Children[0] is Border headerBorder && headerBorder.Child is Grid headerGrid)
                {
                    if (headerGrid.Children[0] is TextBlock headerText)
                    {
                        // Extract name from header text like "🔍 Lost Finding Analysis - 23:36:33"
                        string headerTextContent = headerText.Text;
                        if (headerTextContent.Contains(" - "))
                        {
                            string timestamp = headerTextContent.Split(" - ").Last();
                            // Find the corresponding data by timestamp
                            var dataToRemove = _lostFindingManager.LostFindingData
                                .FirstOrDefault(d => d.Timestamp.ToString("HH:mm:ss") == timestamp);
                            if (dataToRemove != null)
                            {
                                _lostFindingManager.RemoveLostFindingData(dataToRemove.Name);
                                Console.WriteLine($"[NetworkDetailsPage] Removed Lost Finding analysis: {dataToRemove.Name}");
                            }
                        }
                    }
                }
            }
        }

        private void CloseLostFindingSection_Click(object sender, RoutedEventArgs e)
        {
            LostFindingSection.Visibility = Visibility.Collapsed;
        }

        public void HandleLostFindingResponse(string actualImageBase64, string matchedImageBase64, string location, string score, int found, string name = "")
        {
            // This method will be called when we receive a response from the drone
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"[NetworkDetailsPage] 🔍 Handling lost finding response:");
                Console.WriteLine($"[NetworkDetailsPage]   - Name: '{name}'");
                Console.WriteLine($"[NetworkDetailsPage]   - Found: {found}");
                Console.WriteLine($"[NetworkDetailsPage]   - Matched image length: {matchedImageBase64?.Length ?? 0}");
                Console.WriteLine($"[NetworkDetailsPage]   - Actual image length: {actualImageBase64?.Length ?? 0}");
                Console.WriteLine($"[NetworkDetailsPage]   - Total lost finding data items: {_lostFindingManager.LostFindingData.Count}");
                Console.WriteLine($"[NetworkDetailsPage]   - LostFindingSection current visibility: {LostFindingSection.Visibility}");
                
                // Use the persistent manager to handle the response
                _lostFindingManager.HandleResponse(name, matchedImageBase64 ?? "", location ?? "", score ?? "", found);
                
                // Always show the LostFindingSection when we receive a response
                Console.WriteLine($"[NetworkDetailsPage] 📺 Making LostFindingSection visible");
                LostFindingSection.Visibility = Visibility.Visible;
                
                // Force refresh the display to show updated data
                Console.WriteLine($"[NetworkDetailsPage] 🔄 Force refreshing display...");
                ShowAllLostFindingSections();
                
                // Show a notification to the user
                if (found == 1)
                {
                    MessageBox.Show(
                        $"Lost person found!\n\nName: {name}\nLocation: {location}\nScore: {score}%\n\nCheck the Lost Finding section for details.",
                        "Person Found!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Search completed for: {name}\n\nNo match found in the current scan area.\nThe drone will continue searching.",
                        "Search Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            });
        }
    }

    // Data models for network monitoring
    public class NetworkDrone
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public int Battery { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public DateTime LastSeen { get; set; }
    }

    public class NetworkAlert
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Severity { get; set; } = "";
        public string DroneId { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsActive { get; set; }
    }

    public class NetworkCctv
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public bool IsOnline { get; set; }
        public string Resolution { get; set; } = "1080p";
        public int Fps { get; set; } = 30;
        public DateTime LastSeen { get; set; }
    }
}
