using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;
using DroneSurveillanceSystem.Views;
using Microsoft.Win32;
using System.Linq;

namespace DroneSurveillanceSystem.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly SurveillanceService _surveillanceService;
        private readonly DroneTrackingService _droneTrackingService;
        private readonly NetworkService _networkService;
        private readonly Timer _updateTimer;
        private readonly Random _random = new Random();
        private bool _isDetectionRunning = false;
        private int _crowdFreeZonesCount = 0;
        private int _alertCount = 0;

        // Data binding properties
        private string _currentFeedImage = "/Images/default_scene.jpg";
        private string _currentGpsCoordinates = "37.7749, -122.4194";
        private string _currentDroneId = "Drone-001";
        private string _detectionStatus = "No Crowd Detected";
        private string _crowdCountText = "People detected: 0";
        private string _systemStatusText = "System Ready";
        private string _systemStatusMessage = "System Operational - Ready for deployment";
        private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
        private Brush _statusColor = new SolidColorBrush(Colors.Green);
        private Brush _aiStatusColor = new SolidColorBrush(Colors.Orange);
        
        // New dashboard properties
        private string _droneConnectionStatus = "Ready to Connect";
        private Brush _droneConnectionColor = new SolidColorBrush(Colors.Orange);
        private double _batteryLevel = 85.0;
        private string _currentZone = "Zone-A";
        private double _altitude = 50.0;
        private Brush _detectionStatusColor = new SolidColorBrush(Colors.Green);
        private int _peopleCount = 0;
        private bool _isDroneConnected = false;
        
        // Drone tracking integration properties
        private int _activeDronesCount = 0;
        private int _totalCasualties = 0;
        private int _totalAnomalies = 0;
        private string _systemStatusDisplay = "System Ready";

        public ObservableCollection<DetectionEvent> ActiveAlerts { get; set; }
        public ObservableCollection<DetectionEvent> ActivityLog { get; set; }

        // Properties for data binding
        public string CurrentFeedImage
        {
            get => _currentFeedImage;
            set { _currentFeedImage = value; OnPropertyChanged(); }
        }

        public string CurrentGpsCoordinates
        {
            get => _currentGpsCoordinates;
            set { _currentGpsCoordinates = value; OnPropertyChanged(); }
        }

        public string CurrentDroneId
        {
            get => _currentDroneId;
            set { _currentDroneId = value; OnPropertyChanged(); }
        }

        public string DetectionStatus
        {
            get => _detectionStatus;
            set { _detectionStatus = value; OnPropertyChanged(); }
        }

        public string CrowdCountText
        {
            get => _crowdCountText;
            set { _crowdCountText = value; OnPropertyChanged(); }
        }

        public string SystemStatusText
        {
            get => _systemStatusText;
            set { _systemStatusText = value; OnPropertyChanged(); }
        }

        public string SystemStatusMessage
        {
            get => _systemStatusMessage;
            set { _systemStatusMessage = value; OnPropertyChanged(); }
        }

        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public Brush AiStatusColor
        {
            get => _aiStatusColor;
            set { _aiStatusColor = value; OnPropertyChanged(); }
        }

        public string CrowdFreeZonesText => $"Crowd-Free Zones Detected: {_crowdFreeZonesCount}";
        public string AlertCountText => $"Total Alerts: {_alertCount}";
        
        // New dashboard properties
        public string DroneConnectionStatus
        {
            get => _droneConnectionStatus;
            set { _droneConnectionStatus = value; OnPropertyChanged(); }
        }
        
        public Brush DroneConnectionColor
        {
            get => _droneConnectionColor;
            set { _droneConnectionColor = value; OnPropertyChanged(); }
        }
        
        public double BatteryLevel
        {
            get => _batteryLevel;
            set { _batteryLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(BatteryLevelText)); }
        }
        
        public string BatteryLevelText => $"{_batteryLevel:F1}%";
        
        public string CurrentZone
        {
            get => _currentZone;
            set { _currentZone = value; OnPropertyChanged(); }
        }
        
        public double Altitude
        {
            get => _altitude;
            set { _altitude = value; OnPropertyChanged(); OnPropertyChanged(nameof(AltitudeText)); }
        }
        
        public string AltitudeText => $"{_altitude:F1}m";
        
        public Brush DetectionStatusColor
        {
            get => _detectionStatusColor;
            set { _detectionStatusColor = value; OnPropertyChanged(); }
        }
        
        public int PeopleCount
        {
            get => _peopleCount;
            set { _peopleCount = value; OnPropertyChanged(); }
        }
        
        // New properties for drone tracking integration
        public int ActiveDronesCount
        {
            get => _activeDronesCount;
            set { _activeDronesCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveDronesText)); }
        }
        
        public string ActiveDronesText => $"Active Drones: {_activeDronesCount}";
        
        public int TotalCasualties
        {
            get => _totalCasualties;
            set { _totalCasualties = value; OnPropertyChanged(); OnPropertyChanged(nameof(CasualtiesText)); }
        }
        
        public string CasualtiesText => $"Casualties Detected: {_totalCasualties}";
        
        public int TotalAnomalies
        {
            get => _totalAnomalies;
            set { _totalAnomalies = value; OnPropertyChanged(); OnPropertyChanged(nameof(AnomaliesText)); }
        }
        
        public string AnomaliesText => $"Anomalies Detected: {_totalAnomalies}";
        
        public int ActiveAlertsCount => AlertManager.Instance.ActiveAlerts.Count;
        
        public int NetworkActiveDronesCount => _networkService?.Networks
            ?.Where(n => n.Status == "Active" && n.Drones != null)
            ?.Sum(n => n.Drones.Count) ?? 0;
        
        public int TotalDronesCount => _networkService?.Networks
            ?.Where(n => n.Drones != null)
            ?.Sum(n => n.Drones.Count) ?? 0;

        // CCTV aggregates
        public int NetworkActiveCctvsCount => _networkService?.Networks
            ?.Where(n => n.Status == "Active" && n.Cctvs != null)
            ?.Sum(n => n.Cctvs.Count) ?? 0;

        public int TotalCctvsCount => _networkService?.Networks
            ?.Where(n => n.Cctvs != null)
            ?.Sum(n => n.Cctvs.Count) ?? 0;
        
        public string ActiveDronesDisplayText => $"{NetworkActiveDronesCount}/{TotalDronesCount}";
        
        public ObservableCollection<Network> Networks => _networkService?.Networks ?? new ObservableCollection<Network>();
        
        public string SystemStatusDisplay
        {
            get => _systemStatusDisplay;
            set { _systemStatusDisplay = value; OnPropertyChanged(); }
        }

        public MainWindow()
        {
            try
            {
                Console.WriteLine("MainWindow constructor started");
                
                // CRITICAL: Initialize XAML components first
                InitializeComponent();
                Console.WriteLine("InitializeComponent completed");
                
                DataContext = this;
                Console.WriteLine("DataContext set");

                // Initialize collections
                ActiveAlerts = new ObservableCollection<DetectionEvent>();
                ActivityLog = new ObservableCollection<DetectionEvent>();
                Console.WriteLine("Collections initialized");

                // Initialize basic services to prevent null reference exceptions
                Console.WriteLine("Initializing basic services...");
                try 
                {
                    _surveillanceService = new SurveillanceService();
                    Console.WriteLine("SurveillanceService initialized");
                    
                    _droneTrackingService = new DroneTrackingService();
                    Console.WriteLine("DroneTrackingService initialized");
                    
                    _networkService = new NetworkService();
                    Console.WriteLine("NetworkService initialized");
                }
                catch (Exception serviceEx)
                {
                    Console.WriteLine($"Service initialization error: {serviceEx.Message}");
                    // Create minimal fallback services
                    _networkService = new NetworkService();
                }
                
                // Subscribe to drone tracking events (only if service is available)
                if (_droneTrackingService != null)
                {
                    _droneTrackingService.DronePositionUpdated += OnDronePositionUpdated;
                    _droneTrackingService.TrackingAlert += OnDroneAlert;
                    Console.WriteLine("Event subscriptions set");
                }
                
                // Subscribe to AlertManager's ActiveAlerts collection changes
                AlertManager.Instance.ActiveAlerts.CollectionChanged += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(ActiveAlertsCount));
                    });
                };
                
                // Subscribe to NetworkService's Networks collection changes
                if (_networkService != null)
                {
                    _networkService.Networks.CollectionChanged += (s, e) =>
                    {
                        OnPropertyChanged(nameof(Networks));
                        OnPropertyChanged(nameof(NetworkActiveDronesCount));
                        OnPropertyChanged(nameof(TotalDronesCount));
                        OnPropertyChanged(nameof(NetworkActiveCctvsCount));
                        OnPropertyChanged(nameof(TotalCctvsCount));
                        OnPropertyChanged(nameof(ActiveDronesDisplayText));
                    };
                    
                    // Manually trigger Networks property change after initialization
                    OnPropertyChanged(nameof(Networks));
                    OnPropertyChanged(nameof(NetworkActiveDronesCount));
                    OnPropertyChanged(nameof(TotalDronesCount));
                    OnPropertyChanged(nameof(NetworkActiveCctvsCount));
                    OnPropertyChanged(nameof(TotalCctvsCount));
                    OnPropertyChanged(nameof(ActiveDronesDisplayText));
                }
                
                // Initialize some drones for demonstration (only if service is available)
                if (_droneTrackingService != null)
                {
                    Console.WriteLine("Initializing demo data...");
                    _ = InitializeDemoData();
                    
                    // Start drone tracking
                    _droneTrackingService.StartTracking();
                }

                // Setup timer for real-time updates
                _updateTimer = new Timer(UpdateSystem, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

                // Load initial scene
                LoadDefaultScene();
                
                // Add some initial log entries
                AddInitialLogEntries();
                
                Console.WriteLine("MainWindow constructor completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in MainWindow constructor: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error initializing MainWindow: {ex.Message}", "Initialization Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateSystem(object? state)
        {
            Dispatcher.Invoke(() =>
            {
                CurrentTime = DateTime.Now.ToString("HH:mm:ss");
                
                // Update drone tracking data
                UpdateDroneTrackingData();
                
                if (_isDetectionRunning)
                {
                    SimulateDetection();
                }
            });
        }

        private void SimulateDetection()
        {
            // Simulate random crowd detection
            bool crowdDetected = _random.Next(1, 100) <= 15; // 15% chance of crowd detection
            
            if (crowdDetected && DetectionStatus == "No Crowd Detected")
            {
                // Crowd detected
                int crowdSize = _random.Next(3, 25);
                DetectionStatus = "Crowd Detected";
                CrowdCountText = $"People detected: {crowdSize}";
                StatusColor = new SolidColorBrush(Colors.Red);
                SystemStatusMessage = "ALERT: Crowd detected in monitored area";
                
                // Create alert
                var alert = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = GetRandomZone(),
                    Status = $"Crowd Detected ({crowdSize} people)",
                    DroneId = CurrentDroneId,
                    Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.01,
                    Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.01,
                    CrowdCount = crowdSize
                };
                
                ActiveAlerts.Insert(0, alert);
                ActivityLog.Insert(0, alert);
                _alertCount++;
                
                // Update GPS coordinates
                CurrentGpsCoordinates = alert.GpsCoordinates;
                
                OnPropertyChanged(nameof(AlertCountText));
            }
            else if (!crowdDetected && DetectionStatus == "Crowd Detected")
            {
                // Area cleared
                DetectionStatus = "No Crowd Detected";
                CrowdCountText = "People detected: 0";
                StatusColor = new SolidColorBrush(Colors.Green);
                SystemStatusMessage = "Area clear - Continuing surveillance";
                _crowdFreeZonesCount++;
                
                // Create log entry
                var logEntry = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = GetRandomZone(),
                    Status = "Area Clear",
                    DroneId = CurrentDroneId,
                    Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.01,
                    Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.01,
                    CrowdCount = 0
                };
                
                ActivityLog.Insert(0, logEntry);
                OnPropertyChanged(nameof(CrowdFreeZonesText));
                
                // Remove old alerts (keep only last 5)
                while (ActiveAlerts.Count > 5)
                {
                    ActiveAlerts.RemoveAt(ActiveAlerts.Count - 1);
                }
            }

            // Simulate drone movement
            if (_random.Next(1, 100) <= 10) // 10% chance of location change
            {
                CurrentGpsCoordinates = $"{37.7749 + (_random.NextDouble() - 0.5) * 0.02:F6}, {-122.4194 + (_random.NextDouble() - 0.5) * 0.02:F6}";
            }
        }

        private string GetRandomZone()
        {
            string[] zones = { "Zone-A", "Zone-B", "Zone-C", "Zone-D", "Zone-E", "Perimeter-1", "Perimeter-2" };
            return zones[_random.Next(zones.Length)];
        }

        private void LoadDefaultScene()
        {
            // In a real application, this would load an actual image
            // For simulation, we'll create a placeholder or use generated content
            CurrentFeedImage = CreateSimulatedFeedImage();
        }

        private string CreateSimulatedFeedImage()
        {
            // This would typically load from the Images folder
            // For now, return a placeholder path
            return "/Images/surveillance_scene.jpg";
        }

        private void AddInitialLogEntries()
        {
            var initialEntries = new[]
            {
                new DetectionEvent { Timestamp = DateTime.Now.AddMinutes(-5), Zone = "Zone-A", Status = "System Started", DroneId = "Drone-001", Latitude = 37.7749, Longitude = -122.4194 },
                new DetectionEvent { Timestamp = DateTime.Now.AddMinutes(-3), Zone = "Zone-B", Status = "Area Scan Complete", DroneId = "Drone-001", Latitude = 37.7751, Longitude = -122.4196 },
                new DetectionEvent { Timestamp = DateTime.Now.AddMinutes(-1), Zone = "Zone-C", Status = "No Activity Detected", DroneId = "Drone-001", Latitude = 37.7748, Longitude = -122.4192 }
            };

            foreach (var entry in initialEntries)
            {
                ActivityLog.Add(entry);
            }
        }

        // Event handlers
        private void StartStopButton_Click(object sender, RoutedEventArgs e)
        {
            _isDetectionRunning = !_isDetectionRunning;
            
            if (_isDetectionRunning)
            {
                AiStatusColor = new SolidColorBrush(Colors.Green);
                SystemStatusMessage = "AI Detection Active - Monitoring all zones";
                DetectionStatus = "Monitoring Active";
                DetectionStatusColor = new SolidColorBrush(Colors.Green);
                
                var startEntry = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = "System",
                    Status = "Detection Started",
                    DroneId = CurrentDroneId,
                    Latitude = 37.7749,
                    Longitude = -122.4194
                };
                ActivityLog.Insert(0, startEntry);
            }
            else
            {
                AiStatusColor = new SolidColorBrush(Colors.Orange);
                DetectionStatus = "Detection Stopped";
                DetectionStatusColor = new SolidColorBrush(Colors.Orange);
                SystemStatusMessage = "AI Detection Stopped - System on standby";
                
                var stopEntry = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = "System",
                    Status = "Detection Stopped",
                    DroneId = CurrentDroneId,
                    Latitude = 37.7749,
                    Longitude = -122.4194
                };
                ActivityLog.Insert(0, stopEntry);
            }
        }

        private void LoadSceneButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                Title = "Select Surveillance Scene Image"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    CurrentFeedImage = openFileDialog.FileName;
                    
                    var logEntry = new DetectionEvent
                    {
                        Timestamp = DateTime.Now,
                        Zone = "System",
                        Status = "New Scene Loaded",
                        DroneId = CurrentDroneId,
                        Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.01,
                        Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.01
                    };
                    ActivityLog.Insert(0, logEntry);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void DroneTrackingButton_Click(object sender, RoutedEventArgs e)
        {
            var droneTrackingWindow = new DroneTrackingWindow();
            droneTrackingWindow.Owner = this;
            droneTrackingWindow.Show();
        }

        private void NetworkButton_Click(object sender, RoutedEventArgs e)
        {
            var networkMonitoringPage = new NetworkMonitoringPage(_networkService)
            {
                Owner = this,
                WindowState = WindowState.Maximized
            };
            networkMonitoringPage.Show();
        }

        private void MonitoringButton_Click(object sender, RoutedEventArgs e)
        {
            var monitoringWindow = new MonitoringAlertsPage();
            monitoringWindow.Owner = this;
            monitoringWindow.WindowState = WindowState.Maximized;
            monitoringWindow.Show();
        }

        private void ActiveAlertsCard_Click(object sender, MouseButtonEventArgs e)
        {
            var monitoringWindow = new MonitoringAlertsPage();
            monitoringWindow.Owner = this;
            monitoringWindow.WindowState = WindowState.Maximized;
            monitoringWindow.Show();
        }

        private void ActiveDronesCard_Click(object sender, MouseButtonEventArgs e)
        {
            var monitoringWindow = new MonitoringAlertsPage();
            monitoringWindow.Owner = this;
            monitoringWindow.WindowState = WindowState.Maximized;
            monitoringWindow.Show();
        }

        private void ActiveCctvsCard_Click(object sender, MouseButtonEventArgs e)
        {
            var monitoringWindow = new MonitoringAlertsPage();
            monitoringWindow.Owner = this;
            monitoringWindow.WindowState = WindowState.Maximized;
            monitoringWindow.Show();
        }

        private void AdvancedControlButton_Click(object sender, RoutedEventArgs e)
        {
            var controlPanelWindow = new ControlPanelWindow();
            controlPanelWindow.Owner = this;
            controlPanelWindow.Show();
        }

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to sign out?", 
                                       "Confirm Sign Out", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Stop real-time updates and clear current alerts/state
                    try
                    {
                        await DroneSurveillanceSystem.Services.ApiService.Instance.StopWebSocketAsync();
                    }
                    catch { }
                    
                    AlertManager.Instance.ClearAllAlerts();
                    OnPropertyChanged(nameof(ActiveAlertsCount));
                    
                    // Sign out from authentication service
                    var authService = new AuthService();
                    await authService.SignOutAsync();
                    
                    // Show login window again
                    var loginWindow = new LoginWindow(authService);
                    loginWindow.ShowDialog();
                    
                    if (loginWindow.IsAuthenticated || loginWindow.IsGuestMode)
                    {
                        // Reinitialize realtime connection for the new session
                        try
                        {
                            await DroneSurveillanceSystem.Services.ApiService.Instance.StartWebSocketAsync();
                        }
                        catch { }
                        
                        // User signed in again, refresh the main window
                        this.Close();
                        var newMainWindow = new MainWindow();
                        newMainWindow.Show();
                    }
                    else
                    {
                        // User cancelled, exit application
                        _updateTimer?.Dispose();
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Sign out failed: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        // New dashboard event handlers
        private void ConnectDroneButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Connected Drones Page to show USB connected drones in full screen
            ConnectedDronesPage connectedDronesPage = new ConnectedDronesPage();
            connectedDronesPage.WindowState = WindowState.Maximized;
            connectedDronesPage.Show();
            // Keep main window visible or minimize it
        }

        private void AddNetworkButton_Click(object sender, RoutedEventArgs e)
        {
            // Open Network Profile Manager for creating/managing networks
            var networkProfileManager = new NetworkProfileManager(_networkService, _droneTrackingService);
            networkProfileManager.Owner = this;
            networkProfileManager.Show();
        }
        
        private void MonitorDroneButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDroneConnected)
            {
                MessageBox.Show("Please connect a drone first.", "No Drone Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Create detailed monitoring info
            var monitoringInfo = $"Drone Monitoring Details:\n\n" +
                               $"Drone ID: {CurrentDroneId}\n" +
                               $"Current Zone: {CurrentZone}\n" +
                               $"Battery Level: {BatteryLevelText}\n" +
                               $"Altitude: {AltitudeText}\n" +
                               $"GPS Coordinates: {CurrentGpsCoordinates}\n" +
                               $"Status: {DetectionStatus}\n" +
                               $"People Detected: {PeopleCount}";
            
            MessageBox.Show(monitoringInfo, "Drone Monitoring", MessageBoxButton.OK, MessageBoxImage.Information);
            
            var monitorEntry = new DetectionEvent
            {
                Timestamp = DateTime.Now,
                Zone = CurrentZone,
                Status = "Drone Status Checked",
                DroneId = CurrentDroneId,
                Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.01,
                Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.01
            };
            ActivityLog.Insert(0, monitorEntry);
        }

        private async Task InitializeDemoData()
        {
            // Add some demo drones
            await _droneTrackingService.AddDroneToTrackingAsync(new DronePosition { Id = "DRONE-001", Name = "Surveillance Alpha", Latitude = 37.7749, Longitude = -122.4194, Altitude = 50, Status = DroneFlightStatus.Flying });
            await _droneTrackingService.AddDroneToTrackingAsync(new DronePosition { Id = "DRONE-002", Name = "Surveillance Beta", Latitude = 37.7751, Longitude = -122.4196, Altitude = 45, Status = DroneFlightStatus.Hovering });
            await _droneTrackingService.AddDroneToTrackingAsync(new DronePosition { Id = "DRONE-003", Name = "Surveillance Gamma", Latitude = 37.7747, Longitude = -122.4192, Altitude = 55, Status = DroneFlightStatus.Flying });
        }
        
        private void UpdateDroneTrackingData()
        {
            if (_droneTrackingService != null)
            {
                var activeDrones = _droneTrackingService.ActiveDronePositions;
                ActiveDronesCount = activeDrones.Count;
                
                // Calculate total casualties and anomalies
                TotalCasualties = activeDrones.Sum(d => d.CasualtiesDetected);
                TotalAnomalies = activeDrones.Sum(d => d.AnomaliesDetected);
                
                // Update system status based on drone data
                if (activeDrones.Count == 0)
                {
                    SystemStatusDisplay = "No Active Drones";
                }
                else if (TotalCasualties > 0 || TotalAnomalies > 0)
                {
                    SystemStatusDisplay = "ALERT: Incidents Detected";
                }
                else
                {
                    SystemStatusDisplay = "All Systems Operational";
                }
            }
            else
            {
                ActiveDronesCount = 0;
                TotalCasualties = 0;
                TotalAnomalies = 0;
                SystemStatusDisplay = "Service Not Available";
            }
        }
        
        // Drone tracking event handlers
        private void OnDroneAdded(object? sender, DroneTrackingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var logEntry = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = "System",
                    Status = $"Drone {e.DroneId} Added",
                    DroneId = e.DroneId,
                    Latitude = e.Position.Latitude,
                    Longitude = e.Position.Longitude
                };
                ActivityLog.Insert(0, logEntry);
                UpdateDroneTrackingData();
            });
        }
        
        private void OnDroneRemoved(object? sender, DroneTrackingEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var logEntry = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = "System",
                    Status = $"Drone {e.DroneId} Removed",
                    DroneId = e.DroneId,
                    Latitude = e.Position.Latitude,
                    Longitude = e.Position.Longitude
                };
                ActivityLog.Insert(0, logEntry);
                UpdateDroneTrackingData();
            });
        }
        
        private void OnDronePositionUpdated(object? sender, DroneTrackingEventArgs e)
        {
            // Update current drone position if it matches the selected drone
            if (e.DroneId == CurrentDroneId)
            {
                Dispatcher.Invoke(() =>
                {
                    CurrentGpsCoordinates = $"{e.Position.Latitude:F6}, {e.Position.Longitude:F6}";
                    BatteryLevel = e.Position.BatteryLevel;
                    Altitude = e.Position.Altitude;
                });
            }
            
            // Update tracking data for all position updates
            Dispatcher.Invoke(UpdateDroneTrackingData);
        }
        
        private void OnDroneAlert(object? sender, string alertMessage)
        {
            Dispatcher.Invoke(() =>
            {
                // Parse alert message to extract drone ID and alert type
                var parts = alertMessage.Split(new[] { " detected " }, StringSplitOptions.None);
                var droneId = parts.Length > 0 ? parts[0] : "Unknown";
                var alertType = alertMessage.Contains("casualty") ? "Casualty" : "Anomaly";
                
                var alert = new DetectionEvent
                {
                    Timestamp = DateTime.Now,
                    Zone = "Alert Zone",
                    Status = alertMessage,
                    DroneId = droneId,
                    Latitude = 37.7749 + (_random.NextDouble() - 0.5) * 0.01,
                    Longitude = -122.4194 + (_random.NextDouble() - 0.5) * 0.01,
                    CrowdCount = alertType == "Casualty" ? 1 : 0
                };
                
                ActiveAlerts.Insert(0, alert);
                ActivityLog.Insert(0, alert);
                _alertCount++;
                
                OnPropertyChanged(nameof(AlertCountText));
                
                // Update status colors based on alert type
                if (alertType == "Casualty")
                {
                    StatusColor = new SolidColorBrush(Colors.Red);
                    DetectionStatusColor = new SolidColorBrush(Colors.Red);
                }
                else if (alertType == "Anomaly")
                {
                    StatusColor = new SolidColorBrush(Colors.Orange);
                    DetectionStatusColor = new SolidColorBrush(Colors.Orange);
                }
                
                UpdateDroneTrackingData();
            });
        }
        
        protected override void OnClosing(CancelEventArgs e)
        {
            _droneTrackingService?.StopTracking();
            _updateTimer?.Dispose();
            base.OnClosing(e);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
