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
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace DroneSurveillanceSystem.Views
{
    public partial class NetworkDetailsPage : UserControl, INotifyPropertyChanged
    {
        private readonly Network _network;
        private readonly NetworkService _networkService;
        private readonly DispatcherTimer _updateTimer;
        private List<NetworkDrone> _connectedDrones;
        private List<NetworkCctv> _connectedCctvs;
        private List<NetworkAlert> _activeAlerts;

        // Observable collections for data binding (same as MonitoringAlertsPage)
        public ObservableCollection<DeviceDisplayItem> FilteredDevices { get; } = new ObservableCollection<DeviceDisplayItem>();
        public ObservableCollection<AlertData> FilteredAlerts { get; } = new ObservableCollection<AlertData>();

        // Network Summary Properties
        private int _activeDronesCount;
        private double _coverageArea;
        private int _activeCctvsCount;
        private int _totalAlertsCount;
        private double _averageBattery;
        private int _totalCctvsCount;

        public int ActiveDronesCount
        {
            get => _activeDronesCount;
            set { _activeDronesCount = value; OnPropertyChanged(nameof(ActiveDronesCount)); }
        }

        public double CoverageArea
        {
            get => _coverageArea;
            set { _coverageArea = value; OnPropertyChanged(nameof(CoverageArea)); }
        }

        public int ActiveCctvsCount
        {
            get => _activeCctvsCount;
            set { _activeCctvsCount = value; OnPropertyChanged(nameof(ActiveCctvsCount)); }
        }

        public int TotalAlertsCount
        {
            get => _totalAlertsCount;
            set { _totalAlertsCount = value; OnPropertyChanged(nameof(TotalAlertsCount)); }
        }

        public double AverageBattery
        {
            get => _averageBattery;
            set { _averageBattery = value; OnPropertyChanged(nameof(AverageBattery)); }
        }

        public int TotalCctvsCount
        {
            get => _totalCctvsCount;
            set { _totalCctvsCount = value; OnPropertyChanged(nameof(TotalCctvsCount)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public NetworkDetailsPage(Network network)
        {
            InitializeComponent();
            
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _networkService = new NetworkService();
            
            // Initialize collections
            _connectedDrones = new List<NetworkDrone>();
            _connectedCctvs = new List<NetworkCctv>();
            _activeAlerts = new List<NetworkAlert>();
            
            // Set up data context
            DataContext = this;
            
            // Setup UI
            InitializeUI();
            
            // Populate devices and alerts
            PopulateDevices();
            PopulateAlerts();
            UpdateNetworkSummary();
            
            // Subscribe to AlertManager changes for real-time updates
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    PopulateAlerts();
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

        private void PopulateDevices()
        {
            // Clear existing devices
            FilteredDevices.Clear();
            
            // Get all drones and CCTVs from DeviceDataManager (same as MonitoringAlertsPage)
            var allDrones = DeviceDataManager.GetAllDrones();
            var allCctvs = DeviceDataManager.GetAllCctvs();
            
            // Filter devices that belong to this network
            var networkDroneIds = _network.Drones?.Select(d => d.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet() ?? new HashSet<string>();
            var networkCctvIds = _network.Cctvs?.Select(c => c.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet() ?? new HashSet<string>();
            
            // Add drones that belong to this network
            foreach (var drone in allDrones)
            {
                // Check if this drone belongs to the current network
                bool belongsToNetwork = networkDroneIds.Contains(drone.DeviceId) || 
                                      _network.Drones?.Any(nd => nd.Name == drone.Name) == true;
                
                if (belongsToNetwork)
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = drone.Name,
                        Details = $"Device ID: {drone.DeviceId}",
                        AdditionalInfo = $"USB Port: {drone.UsbPort} | Firmware: {drone.FirmwareVersion}",
                        Status = drone.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusText = string.IsNullOrWhiteSpace(drone.Status) ? (drone.IsConnected ? "Connected" : "Disconnected") : drone.Status,
                        BatteryText = $"Battery: {Math.Max(0, Math.Min(100, drone.BatteryLevel))}%",
                        StatusColor = drone.IsConnected ? "#88C999" : "#dc3545",
                        DeviceId = drone.DeviceId,
                        DeviceType = "Drone"
                    });
                }
            }
            
            // Add CCTVs that belong to this network
            foreach (var cctv in allCctvs)
            {
                // Check if this CCTV belongs to the current network
                bool belongsToNetwork = networkCctvIds.Contains(cctv.DeviceId) || 
                                      _network.Cctvs?.Any(nc => nc.Name == cctv.Name) == true;
                
                if (belongsToNetwork)
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = cctv.Name,
                        Details = $"Device ID: {cctv.DeviceId}",
                        AdditionalInfo = $"Resolution: {cctv.Resolution} | Frame Rate: {cctv.FrameRate}fps",
                        Status = cctv.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusColor = cctv.IsConnected ? "#88C999" : "#dc3545",
                        DeviceId = cctv.DeviceId,
                        DeviceType = "CCTV"
                    });
                }
            }
            
            // Update devices title
            DevicesTitle.Text = $"🚁📷 {_network.Name} Devices ({FilteredDevices.Count} Total)";
            
            // Notify UI of changes
            OnPropertyChanged(nameof(FilteredDevices));
        }









        private void PopulateAlerts()
        {
            // Clear existing alerts
            FilteredAlerts.Clear();
            
            // Get all alerts from AlertManager
            var allAlerts = AlertManager.Instance.GetAllDeviceAlerts();
            
            // Get device IDs that belong to this network
            var networkDeviceIds = new HashSet<string>();
            
            // Add drone IDs
            if (_network.Drones != null)
            {
                foreach (var drone in _network.Drones)
                {
                    if (!string.IsNullOrEmpty(drone.Id))
                        networkDeviceIds.Add(drone.Id);
                    
                    // Also add by name matching
                    var matchingDrone = DeviceDataManager.GetAllDrones().FirstOrDefault(d => d.Name == drone.Name);
                    if (matchingDrone != null)
                        networkDeviceIds.Add(matchingDrone.DeviceId);
                }
            }
            
            // Add CCTV IDs
            if (_network.Cctvs != null)
            {
                foreach (var cctv in _network.Cctvs)
                {
                    if (!string.IsNullOrEmpty(cctv.Id))
                        networkDeviceIds.Add(cctv.Id);
                    
                    // Also add by name matching
                    var matchingCctv = DeviceDataManager.GetAllCctvs().FirstOrDefault(c => c.Name == cctv.Name);
                    if (matchingCctv != null)
                        networkDeviceIds.Add(matchingCctv.DeviceId);
                }
            }
            
            // Filter alerts for devices in this network
            foreach (var alert in allAlerts)
            {
                if (networkDeviceIds.Contains(alert.DroneId ?? ""))
                {
                    FilteredAlerts.Add(alert);
                }
            }
            
            // Notify UI of changes
            OnPropertyChanged(nameof(FilteredAlerts));
        }

        private void UpdateNetworkSummary()
        {
            // Get all devices for this network
            var allDrones = DeviceDataManager.GetAllDrones();
            var allCctvs = DeviceDataManager.GetAllCctvs();
            
            // Get network device IDs
            var networkDroneIds = _network.Drones?.Select(d => d.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet() ?? new HashSet<string>();
            var networkCctvIds = _network.Cctvs?.Select(c => c.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet() ?? new HashSet<string>();
            
            // Calculate active drones count
            var networkDrones = allDrones.Where(d => 
                networkDroneIds.Contains(d.DeviceId) || 
                _network.Drones?.Any(nd => nd.Name == d.Name) == true).ToList();
            ActiveDronesCount = networkDrones.Count(d => d.IsConnected);
            
            // Calculate total CCTVs count
            var networkCctvs = allCctvs.Where(c => 
                networkCctvIds.Contains(c.DeviceId) || 
                _network.Cctvs?.Any(nc => nc.Name == c.Name) == true).ToList();
            TotalCctvsCount = networkCctvs.Count;
            ActiveCctvsCount = networkCctvs.Count(c => c.IsConnected);
            
            // Calculate average battery level (only among connected drones)
            var connectedDrones = networkDrones.Where(d => d.IsConnected).ToList();
            AverageBattery = connectedDrones.Any() ? connectedDrones.Average(d => d.BatteryLevel) : 0;
            
            // Calculate coverage area (simplified calculation based on number of active devices)
            // Each active drone covers approximately 2.5 km², each CCTV covers 0.5 km²
            CoverageArea = (ActiveDronesCount * 2.5) + (ActiveCctvsCount * 0.5);
            
            // Calculate total alerts count
            TotalAlertsCount = FilteredAlerts.Count;
        }

        private void Alert_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string alertTag)
            {
                var alert = AlertManager.Instance.ActiveAlerts.FirstOrDefault(a => a.Timestamp == alertTag);
                if (alert != null)
                {
                    /* EXAMPLE: How to use the loading indicator during validation:
                     * 
                     * // Show loading indicator
                     * ShowValidationLoading();
                     * 
                     * // Simulate validation task (replace with actual validation code)
                     * await Task.Delay(3000); // Simulates long-running validation
                     * 
                     * // Hide loading indicator when done
                     * HideValidationLoading();
                     */

                    var alertPopup = new AlertInfoPopup(alert);
                    
                    // Get the parent window instead of using 'this'
                    var parentWindow = Window.GetWindow(this);
                    if (parentWindow != null)
                    {
                        alertPopup.Owner = parentWindow;
                    }
                    
                    alertPopup.ShowDialog();
                }
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Refresh devices and alerts periodically
            PopulateDevices();
            PopulateAlerts();
            UpdateNetworkSummary();
        }

        private void AcknowledgeAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get alerts for this network only
                var networkAlerts = FilteredAlerts.ToList();
                
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Stop the timer to prevent memory leaks
            _updateTimer?.Stop();
            
            // Raise the event to notify the parent to close this page
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // ==================== VALIDATION LOADING INDICATOR METHODS ====================
        
        /// <summary>
        /// Shows the loading indicator during validation process
        /// </summary>
        public void ShowValidationLoading()
        {
            if (ValidationLoadingOverlay != null)
            {
                ValidationLoadingOverlay.Visibility = Visibility.Visible;
                StartLoadingAnimation();
            }
        }

        /// <summary>
        /// Hides the loading indicator when validation completes
        /// </summary>
        public void HideValidationLoading()
        {
            if (ValidationLoadingOverlay != null)
            {
                ValidationLoadingOverlay.Visibility = Visibility.Collapsed;
                StopLoadingAnimation();
            }
        }

        /// <summary>
        /// Starts the animated loading circles
        /// </summary>
        private void StartLoadingAnimation()
        {
            var animationTimer = new DispatcherTimer();
            animationTimer.Interval = TimeSpan.FromMilliseconds(150);
            int animationStep = 0;

            animationTimer.Tick += (s, e) =>
            {
                if (ValidationLoadingOverlay?.Visibility != Visibility.Visible)
                {
                    animationTimer.Stop();
                    return;
                }

                animationStep = (animationStep + 1) % 6;

                // Animate circles with scale effect
                if (LoadingCircle1 != null && LoadingCircle1.RenderTransform is ScaleTransform scale1)
                {
                    scale1.ScaleX = scale1.ScaleY = animationStep == 0 ? 1.2 : 1.0;
                }
                if (LoadingCircle2 != null && LoadingCircle2.RenderTransform is ScaleTransform scale2)
                {
                    scale2.ScaleX = scale2.ScaleY = animationStep == 2 ? 1.2 : 1.0;
                }
                if (LoadingCircle3 != null && LoadingCircle3.RenderTransform is ScaleTransform scale3)
                {
                    scale3.ScaleX = scale3.ScaleY = animationStep == 4 ? 1.2 : 1.0;
                }
            };

            animationTimer.Start();
            Tag = animationTimer; // Store timer reference to stop it later
        }

        /// <summary>
        /// Stops the animated loading circles
        /// </summary>
        private void StopLoadingAnimation()
        {
            if (Tag is DispatcherTimer timer)
            {
                timer.Stop();
            }
        }

        // Event to notify when the network details page should be closed
        public event EventHandler? CloseRequested;
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