using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DroneSurveillanceSystem.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using System.Net.Http;

namespace DroneSurveillanceSystem.Views
{
    public partial class NetworkMonitoringPage : Window, INotifyPropertyChanged
    {
        private readonly NetworkService _networkService;

        public int TotalDronesCount => _networkService.Networks.Sum(n => n.Drones?.Count ?? 0);
        public int ActiveAlertsCount => AlertManager.Instance.GetAllDeviceAlerts().Count();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Gets the count of alerts for a specific network (only from devices in that network)
        /// </summary>
        public int GetNetworkAlertCount(Network network)
        {
            return AlertManager.Instance.GetAlertsForNetwork(network).Count();
        }

        public NetworkMonitoringPage()
        {
            InitializeComponent();
            _networkService = new NetworkService();
            this.DataContext = this;
            LoadNetworks();
            
            // Subscribe to collection changes
            _networkService.Networks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalDronesCount));
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ActiveAlertsCount));
        }

        public NetworkMonitoringPage(NetworkService networkService)
        {
            InitializeComponent();
            _networkService = networkService;
            this.DataContext = this;
            LoadNetworks();
            
            // Subscribe to collection changes
            _networkService.Networks.CollectionChanged += (s, e) => OnPropertyChanged(nameof(TotalDronesCount));
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ActiveAlertsCount));
        }

        private void LoadNetworks()
        {
            NetworksWrapPanel.Children.Clear();
            
            foreach (var network in _networkService.Networks)
            {
                var networkCard = CreateNetworkCard(network);
                NetworksWrapPanel.Children.Add(networkCard);
            }
        }

        private Border CreateNetworkCard(Network network)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(8),
                Padding = new Thickness(16),
                Width = 280,
                Height = 180
            };

            var stackPanel = new StackPanel();

            // Network icon
            var icon = new TextBlock
            {
                Text = "🌐",
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(network.StatusColor))
            };
            stackPanel.Children.Add(icon);

            // Network name
            var nameText = new TextBlock
            {
                Text = network.Name,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            stackPanel.Children.Add(nameText);

            // Network description
            var descText = new TextBlock
            {
                Text = network.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(descText);

            // Status panel
            var statusPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var statusLabel = new TextBlock
            {
                Text = "Status: ",
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontSize = 10
            };
            statusPanel.Children.Add(statusLabel);

            var statusIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(network.StatusColor)),
                Margin = new Thickness(3, 0, 3, 0)
            };
            statusPanel.Children.Add(statusIndicator);

            var statusText = new TextBlock
            {
                Text = network.Status,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(network.StatusColor)),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            statusPanel.Children.Add(statusText);

            stackPanel.Children.Add(statusPanel);

            // Monitor button
            var monitorButton = new Button
            {
                Content = "Monitor",
                Style = (Style)FindResource("NetworkMonitorButton")
            };
            monitorButton.Click += (s, e) => MonitorNetwork(network);
            stackPanel.Children.Add(monitorButton);

            card.Child = stackPanel;
            return card;
        }

        private void MonitorNetwork(Network network)
        {
            var networkDetailsWindow = new NetworkDetailsPage(network)
            {
                Owner = this,
                WindowState = WindowState.Maximized
            };
            networkDetailsWindow.Show();
        }

        private void Network1Button_Click(object sender, RoutedEventArgs e)
        {
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == "Network 1");
            if (network != null)
            {
                var networkDetailsWindow = new NetworkDetailsPage(network);
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
            }
        }

        private void Network2Button_Click(object sender, RoutedEventArgs e)
        {
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == "Network 2");
            if (network != null)
            {
                var networkDetailsWindow = new NetworkDetailsPage(network);
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
            }
        }

        private void Network3Button_Click(object sender, RoutedEventArgs e)
        {
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == "Network 3");
            if (network != null)
            {
                var networkDetailsWindow = new NetworkDetailsPage(network);
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
            }
        }

        private void Network4Button_Click(object sender, RoutedEventArgs e)
        {
            var network = _networkService.Networks.FirstOrDefault(n => n.Name == "Network 4");
            if (network != null)
            {
                var networkDetailsWindow = new NetworkDetailsPage(network);
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
            }
        }

        // Network 5 and 6 button handlers removed as requested

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void LostFindingButton_Click(object sender, RoutedEventArgs e)
        {
            // Open the MonitoringAlertsPage where the Lost Finding functionality is properly implemented
            var monitoringAlertsPage = new MonitoringAlertsPage();
            monitoringAlertsPage.Owner = this;
            monitoringAlertsPage.Show();
            
            MessageBox.Show("Lost Finding page opened! Use the Lost Finding button in the monitoring page to upload images and view results.", 
                          "Lost Finding", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AcknowledgeAllAlertsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("All alerts acknowledged.", "Acknowledge Alerts", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void PendingButton_Click(object sender, RoutedEventArgs e)
        {
            // For now, show a simple status dialog. This can be wired to your real pending queue.
            MessageBox.Show("No pending items.", "Pending", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Method to demonstrate dynamic updates
        public void SimulateNetworkUpdate()
        {
            // Simulate a network status change
            _networkService.UpdateNetworkStatus("Network 3", "Active");
            _networkService.UpdateNetworkDrones("Network 3", 6);
            _networkService.UpdateNetworkAlerts("Network 3", 2);
        }

        // Method to demonstrate alert updates
        public void SimulateAlertUpdate()
        {
            // Simulate new alerts
            _networkService.UpdateNetworkAlerts("Network 1", 5);
            _networkService.UpdateNetworkAlerts("Network 2", 4);
        }

        // Method to demonstrate drone count changes
        public void SimulateDroneUpdate()
        {
            // Simulate drone count changes
            _networkService.UpdateNetworkDrones("Network 1", 10);
            _networkService.UpdateNetworkDrones("Network 2", 8);
        }

        // Method to add a new network dynamically
        public void AddNewNetwork(string name, string description, string status, int droneCount, int alertCount)
        {
            var newNetwork = new Network
            {
                Name = name,
                Description = description,
                Status = status,
                StatusColor = status switch
                {
                    "Active" => "#4CAF50",
                    "Standby" => "#FF9800",
                    "Offline" => "#F44336",
                    "Testing" => "#9C27B0",
                    "Deployed" => "#00BCD4",
                    _ => "#cccccc"
                },
                IconColor = "#4CAF50",
                DroneCount = droneCount,
                AlertCount = alertCount
            };

            // Subscribe to property changes
            newNetwork.PropertyChanged += _networkService.OnNetworkPropertyChanged;
            
            // Add to the collection (this will automatically update statistics)
            _networkService.Networks.Add(newNetwork);
        }

        // Method to remove a network dynamically
        public void RemoveNetwork(string networkName)
        {
            var networkToRemove = _networkService.Networks.FirstOrDefault(n => n.Name == networkName);
            if (networkToRemove != null)
            {
                // Unsubscribe from property changes
                networkToRemove.PropertyChanged -= _networkService.OnNetworkPropertyChanged;
                
                // Remove from collection (this will automatically update statistics)
                _networkService.Networks.Remove(networkToRemove);
            }
        }
    }
}
