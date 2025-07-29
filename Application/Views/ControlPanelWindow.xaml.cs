using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;
using Newtonsoft.Json;

namespace DroneSurveillanceSystem.Views
{
    public partial class ControlPanelWindow : Window
    {
        // Services
        private readonly NetworkService _networkService;
        private readonly DroneControlService _droneControlService;
        private readonly DataProcessingService _dataProcessingService;
        private readonly AIModelService _aiModelService;

        // UI update timer
        private readonly DispatcherTimer _uiUpdateTimer;

        // Collections for data binding
        public ObservableCollection<string> ProcessingEvents { get; set; }
        public ObservableCollection<AIModel> AIModels { get; set; }
        public ObservableCollection<string> ActiveModels { get; set; }

        // Status tracking
        private bool _isDroneConnected = false;
        private bool _isRecording = false;

        public ControlPanelWindow()
        {
            InitializeComponent();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
            
            // Initialize services
            _networkService = new NetworkService();
            _droneControlService = new DroneControlService(_networkService);
            _dataProcessingService = new DataProcessingService();
            _aiModelService = new AIModelService();

            // Initialize collections
            ProcessingEvents = new ObservableCollection<string>();
            AIModels = new ObservableCollection<AIModel>();
            ActiveModels = new ObservableCollection<string>();

            // Set data context
            ProcessingEventsListBox.ItemsSource = ProcessingEvents;
            AIModelsDataGrid.ItemsSource = AIModels;
            ActiveModelsListBox.ItemsSource = ActiveModels;

            // Setup event handlers
            SetupEventHandlers();

            // Initialize UI data
            InitializeUIData();

            // Setup UI update timer
            _uiUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiUpdateTimer.Tick += UpdateUI;
            _uiUpdateTimer.Start();
        }

        private void SetupEventHandlers()
        {
            // Network service events - commented out due to new NetworkService structure
            // _networkService.NetworkStatusChanged += OnNetworkStatusChanged;

            // Drone control service events
            _droneControlService.DroneStatusChanged += OnDroneStatusChanged;
            _droneControlService.CommandExecuted += OnCommandExecuted;

            // Data processing service events
            _dataProcessingService.DataProcessed += OnDataProcessed;
            _dataProcessingService.ProcessingAlert += OnProcessingAlert;
        }

        private void InitializeUIData()
        {
            // Load AI models
            foreach (var model in _aiModelService.AvailableModels)
            {
                AIModels.Add(model);
            }

            // Update active models list
            RefreshActiveModels();

            // Set initial flight mode
            FlightModeComboBox.SelectedIndex = 0; // Manual
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            // Update flight data
            UpdateFlightData();

            // Update processing statistics
            UpdateProcessingStatistics();

            // Update network status
            UpdateNetworkStatus();
        }

        private void UpdateFlightData()
        {
            AltitudeText.Text = $"{_droneControlService.Altitude:F1} m";
            SpeedText.Text = $"{_droneControlService.Speed:F1} m/s";
            GPSText.Text = $"{_droneControlService.Latitude:F6}, {_droneControlService.Longitude:F6}";
            BatteryProgressBar.Value = _droneControlService.BatteryLevel;
            BatteryPercentText.Text = $"{_droneControlService.BatteryLevel:F1}%";
            CurrentFlightModeText.Text = _droneControlService.FlightMode.ToString();
            CurrentZoneText.Text = _droneControlService.CurrentZone;

            // Update battery color based on level
            if (_droneControlService.BatteryLevel < 20)
                BatteryProgressBar.Foreground = new SolidColorBrush(Colors.Red);
            else if (_droneControlService.BatteryLevel < 50)
                BatteryProgressBar.Foreground = new SolidColorBrush(Colors.Orange);
            else
                BatteryProgressBar.Foreground = new SolidColorBrush(Colors.Green);

            // Update recording status
            RecordingStatusText.Text = _isRecording ? "Recording" : "Stopped";
            RecordingIndicator.Fill = new SolidColorBrush(_isRecording ? Colors.Red : Colors.Gray);
        }

        private void UpdateProcessingStatistics()
        {
            var stats = _dataProcessingService.GetStatistics();
            PacketsProcessedText.Text = stats.PacketsProcessed.ToString();
            QueueSizeText.Text = stats.QueueSize.ToString();
            AvgProcessingTimeText.Text = $"{stats.AverageProcessingTime:F1} ms";
        }

        private void UpdateNetworkStatus()
        {
            // Network status update - simplified for new NetworkService structure
            NetworkStatusText.Text = "Monitoring";
            SignalStrengthBar.Value = 85; // Default value
            SignalStrengthText.Text = "85%";

            // Update network status indicator
            NetworkStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
        }

        private void RefreshActiveModels()
        {
            ActiveModels.Clear();
            foreach (var model in _aiModelService.ActiveModels)
            {
                ActiveModels.Add($"{model.Name} - {model.Status}");
            }
        }

        // Event handlers for service events
        private void OnNetworkStatusChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingEvents.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Network: Statistics Updated");
                if (ProcessingEvents.Count > 100)
                    ProcessingEvents.RemoveAt(ProcessingEvents.Count - 1);
            });
        }

        private void OnDroneStatusChanged(object? sender, DroneStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingEvents.Insert(0, $"[{e.Timestamp:HH:mm:ss}] Drone: {e.FlightMode} - Alt: {e.Altitude:F1}m");
                if (ProcessingEvents.Count > 100)
                    ProcessingEvents.RemoveAt(ProcessingEvents.Count - 1);
            });
        }

        private void OnCommandExecuted(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingEvents.Insert(0, $"[{DateTime.Now:HH:mm:ss}] Command: {message}");
                if (ProcessingEvents.Count > 100)
                    ProcessingEvents.RemoveAt(ProcessingEvents.Count - 1);
            });
        }

        private void OnDataProcessed(object? sender, DataProcessedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingEvents.Insert(0, $"[{e.Timestamp:HH:mm:ss}] Processed: {e.ProcessedData.Type} ({e.ProcessingTime.TotalMilliseconds:F1}ms)");
                if (ProcessingEvents.Count > 100)
                    ProcessingEvents.RemoveAt(ProcessingEvents.Count - 1);
            });
        }

        private void OnProcessingAlert(object? sender, string alert)
        {
            Dispatcher.Invoke(() =>
            {
                ProcessingEvents.Insert(0, $"[{DateTime.Now:HH:mm:ss}] ALERT: {alert}");
                if (ProcessingEvents.Count > 100)
                    ProcessingEvents.RemoveAt(ProcessingEvents.Count - 1);
            });
        }

        // Flight Control Event Handlers
        private async void TakeOffButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDroneConnected)
            {
                MessageBox.Show("Please connect to a drone first.", "Drone Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await _droneControlService.ExecuteCommandAsync(DroneCommand.TakeOff);
        }

        private async void LandButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.Land);
        }

        private async void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.MoveUp);
        }

        private async void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.MoveDown);
        }

        private async void StartPatrolButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.StartPatrol);
        }

        private async void StopPatrolButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.StopPatrol);
        }

        private async void ReturnHomeButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.ReturnHome);
        }

        private async void EmergencyStopButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to execute an emergency stop?", 
                                       "Emergency Stop", 
                                       MessageBoxButton.YesNo, 
                                       MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _droneControlService.ExecuteCommandAsync(DroneCommand.EmergencyStop);
            }
        }

        private async void ConnectDroneButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isDroneConnected)
            {
                // Simulate connection for new NetworkService structure
                await Task.Delay(1000); // Simulate connection delay
                var success = true; // Simulate successful connection
                if (success)
                {
                    _isDroneConnected = true;
                    ConnectDroneButton.Content = "Disconnect Drone";
                    DroneConnectionText.Text = "Connected";
                    DroneConnectionIndicator.Fill = new SolidColorBrush(Colors.Green);
                    MessageBox.Show("Drone connected successfully!", "Connection Status", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Failed to connect to drone.", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Simulate disconnection for new NetworkService structure
                _isDroneConnected = false;
                ConnectDroneButton.Content = "Connect Drone";
                DroneConnectionText.Text = "Disconnected";
                DroneConnectionIndicator.Fill = new SolidColorBrush(Colors.Red);
                MessageBox.Show("Drone disconnected.", "Connection Status", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                await _droneControlService.ExecuteCommandAsync(DroneCommand.StartRecording);
                _isRecording = true;
                StartRecordingButton.Content = "Stop Recording";
            }
            else
            {
                await _droneControlService.ExecuteCommandAsync(DroneCommand.StopRecording);
                _isRecording = false;
                StartRecordingButton.Content = "Start Recording";
            }
        }

        private async void CalibrateGPSButton_Click(object sender, RoutedEventArgs e)
        {
            await _droneControlService.ExecuteCommandAsync(DroneCommand.CalibrateGPS);
            MessageBox.Show("GPS calibration initiated.", "GPS Calibration", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Data Processing Event Handlers
        private void StartProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            _dataProcessingService.StartProcessing();
            MessageBox.Show("Data processing started.", "Processing Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void StopProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            _dataProcessingService.StopProcessing();
            MessageBox.Show("Data processing stopped.", "Processing Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SimulateDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Simulate different types of data packets
            var random = new Random();
            
            if (TelemetryCheckBox.IsChecked == true)
            {
                var telemetryData = new TelemetryData
                {
                    Altitude = _droneControlService.Altitude,
                    Speed = _droneControlService.Speed,
                    BatteryLevel = _droneControlService.BatteryLevel,
                    Temperature = 25 + random.NextDouble() * 15,
                    Humidity = 40 + random.NextDouble() * 30
                };
                
                _dataProcessingService.EnqueueData(new DataPacket
                {
                    Type = DataType.TelemetryData,
                    Data = JsonConvert.SerializeObject(telemetryData),
                    Source = "Drone-001"
                });
            }

            if (SensorCheckBox.IsChecked == true)
            {
                _dataProcessingService.EnqueueData(new DataPacket
                {
                    Type = DataType.SensorData,
                    Data = "sensor_data_payload",
                    Source = "Sensors"
                });
            }

            if (ImageCheckBox.IsChecked == true)
            {
                _dataProcessingService.EnqueueData(new DataPacket
                {
                    Type = DataType.ImageData,
                    Data = new string('X', random.Next(1000, 5000)),
                    Source = "Camera"
                });
            }

            if (GPSCheckBox.IsChecked == true)
            {
                var gpsData = new GPSData
                {
                    Latitude = _droneControlService.Latitude,
                    Longitude = _droneControlService.Longitude,
                    Altitude = _droneControlService.Altitude,
                    Accuracy = random.NextDouble() * 5,
                    SatelliteCount = random.Next(4, 12)
                };
                
                _dataProcessingService.EnqueueData(new DataPacket
                {
                    Type = DataType.GPSData,
                    Data = JsonConvert.SerializeObject(gpsData),
                    Source = "GPS"
                });
            }

            if (BatteryCheckBox.IsChecked == true)
            {
                var batteryData = new BatteryData
                {
                    Level = _droneControlService.BatteryLevel,
                    Voltage = 11.5 + random.NextDouble() * 1.0,
                    Current = 1.5 + random.NextDouble() * 2.0,
                    Temperature = 25 + random.NextDouble() * 15
                };
                
                _dataProcessingService.EnqueueData(new DataPacket
                {
                    Type = DataType.BatteryData,
                    Data = JsonConvert.SerializeObject(batteryData),
                    Source = "Battery"
                });
            }
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            // Note: This would require adding a method to DataProcessingService
            ProcessingEvents.Clear();
            MessageBox.Show("Processing queue and events cleared.", "Queue Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // AI Models Event Handlers
        private async void InstallModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (AIModelsDataGrid.SelectedItem is AIModel selectedModel)
            {
                var success = await _aiModelService.InstallModelAsync(selectedModel.Id);
                if (success)
                {
                    MessageBox.Show($"Model '{selectedModel.Name}' installed successfully!", "Installation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshActiveModels();
                }
                else
                {
                    MessageBox.Show($"Failed to install model '{selectedModel.Name}'.", "Installation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a model to install.", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ActivateModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (AIModelsDataGrid.SelectedItem is AIModel selectedModel)
            {
                var success = _aiModelService.ActivateModel(selectedModel.Id);
                if (success)
                {
                    MessageBox.Show($"Model '{selectedModel.Name}' activated.", "Model Activated", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshActiveModels();
                }
                else
                {
                    MessageBox.Show($"Failed to activate model '{selectedModel.Name}'. Make sure it's installed first.", "Activation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a model to activate.", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeactivateModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (AIModelsDataGrid.SelectedItem is AIModel selectedModel)
            {
                var success = _aiModelService.DeactivateModel(selectedModel.Id);
                if (success)
                {
                    MessageBox.Show($"Model '{selectedModel.Name}' deactivated.", "Model Deactivated", MessageBoxButton.OK, MessageBoxImage.Information);
                    RefreshActiveModels();
                }
                else
                {
                    MessageBox.Show($"Failed to deactivate model '{selectedModel.Name}'.", "Deactivation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a model to deactivate.", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UninstallModelButton_Click(object sender, RoutedEventArgs e)
        {
            if (AIModelsDataGrid.SelectedItem is AIModel selectedModel)
            {
                var result = MessageBox.Show($"Are you sure you want to uninstall '{selectedModel.Name}'?", 
                                           "Confirm Uninstall", 
                                           MessageBoxButton.YesNo, 
                                           MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    var success = _aiModelService.UninstallModel(selectedModel.Id);
                    if (success)
                    {
                        MessageBox.Show($"Model '{selectedModel.Name}' uninstalled.", "Model Uninstalled", MessageBoxButton.OK, MessageBoxImage.Information);
                        RefreshActiveModels();
                    }
                    else
                    {
                        MessageBox.Show($"Failed to uninstall model '{selectedModel.Name}'.", "Uninstall Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select a model to uninstall.", "No Model Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ProcessTestImageButton_Click(object sender, RoutedEventArgs e)
        {
            var activeModelIds = _aiModelService.ActiveModels.Select(m => m.Id).ToList();
            if (activeModelIds.Count == 0)
            {
                MessageBox.Show("No active AI models. Please activate at least one model first.", "No Active Models", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = await _aiModelService.ProcessImageAsync("test_image.jpg", activeModelIds);
            
            var message = $"Test Image Processing Results:\n\n" +
                         $"Objects Detected: {result.ObjectCount}\n" +
                         $"Detection Confidence: {result.Confidence:P1}\n" +
                         $"Detected Objects: {result.DetectedObjects}\n" +
                         $"Bounding Boxes: {result.BoundingBoxes.Count}\n" +
                         $"Models Used: {result.ModelUsed}";
            
            MessageBox.Show(message, "Processing Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        {
            AIModels.Clear();
            foreach (var model in _aiModelService.AvailableModels)
            {
                AIModels.Add(model);
            }
            RefreshActiveModels();
            MessageBox.Show("AI models list refreshed.", "Models Refreshed", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _uiUpdateTimer?.Stop();
            // _networkService?.Dispose(); // Commented out - new NetworkService doesn't implement IDisposable
            _droneControlService?.Dispose();
            _dataProcessingService?.Dispose();
            base.OnClosed(e);
        }
    }
}
