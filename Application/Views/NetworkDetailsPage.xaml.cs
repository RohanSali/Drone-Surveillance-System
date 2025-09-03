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
            
            // Setup UI
            InitializeUI();
            
            // Generate data based on actual network
            GenerateNetworkData();
            
            // Populate UI with data
            PopulateDrones();
            PopulateCctvs();
            PopulateAlerts();
            UpdateNetworkSummary();
            
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
            _connectedDrones.Clear();
            _connectedCctvs.Clear();
            _activeAlerts.Clear();

            // Populate drones using actual assigned drones from the network
            if (_network.Drones != null)
            {
                // Lookup table from DeviceDataManager for real device IDs
                var allDrones = DeviceDataManager.GetAllDrones();

                foreach (var assigned in _network.Drones)
                {
                    if (assigned == null) continue;

                    // Resolve a real device id: prefer assigned.Id, else map by Name from DeviceDataManager
                    string resolvedId = !string.IsNullOrWhiteSpace(assigned.Id)
                        ? assigned.Id
                        : allDrones.FirstOrDefault(d => string.Equals(d.Name, assigned.Name, StringComparison.OrdinalIgnoreCase))?.DeviceId
                          ?? allDrones.FirstOrDefault(d => assigned.Name != null && d.Name.Contains(assigned.Name, StringComparison.OrdinalIgnoreCase))?.DeviceId
                          ?? string.Empty;

                    var drone = new NetworkDrone
                    {
                        Id = resolvedId,
                        Name = assigned.Name ?? "Unknown Drone",
                        Status = assigned.StatusText,
                        Battery = (int)Math.Round(assigned.BatteryLevel),
                        Latitude = assigned.Latitude,
                        Longitude = assigned.Longitude,
                        Altitude = assigned.Altitude,
                        LastSeen = assigned.LastSeen
                    };
                    _connectedDrones.Add(drone);
                }
            }

            // If no drones are assigned, show placeholder card
            if (_connectedDrones.Count == 0)
            {
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

            // Populate CCTVs using actual assigned CCTVs from the network
            if (_network.Cctvs != null)
            {
                // Lookup table from DeviceDataManager for real device IDs and status
                var allCctvs = DeviceDataManager.GetAllCctvs();

                foreach (var cam in _network.Cctvs)
                {
                    if (cam == null) continue;

                    var match = allCctvs.FirstOrDefault(c => string.Equals(c.Name, cam.Name, StringComparison.OrdinalIgnoreCase))
                               ?? allCctvs.FirstOrDefault(c => cam.Name != null && c.Name.Contains(cam.Name, StringComparison.OrdinalIgnoreCase));

                    var cctv = new NetworkCctv
                    {
                        Id = match?.DeviceId ?? cam.Id ?? string.Empty,
                        Name = cam.Name ?? "Unknown CCTV",
                        IsOnline = match?.IsConnected ?? true,
                        Resolution = match?.Resolution ?? string.Empty,
                        Fps = match?.FrameRate ?? 0,
                        LastSeen = DateTime.Now
                    };
                    _connectedCctvs.Add(cctv);
                }
            }

            // Alerts: use AlertManager filtered by this network (already done in PopulateAlerts)
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

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
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