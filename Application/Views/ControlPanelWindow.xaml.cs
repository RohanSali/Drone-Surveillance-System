using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;
using Newtonsoft.Json;
using System.Globalization;

namespace DroneSurveillanceSystem.Views
{
    public partial class ControlPanelWindow : Window
    {
        // Services
        private readonly NetworkService _networkService;
        private readonly DroneControlService _droneControlService;

        // UI update timer
        private readonly DispatcherTimer _uiUpdateTimer;

        // Status tracking
        private bool _isDroneConnected = false;
        private bool _isRecording = false;

        private string _selectedDroneId = "";
        private Button? _selectedDroneButton;

        public ControlPanelWindow()
        {
            InitializeComponent();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
            
            // Initialize services
            _networkService = new NetworkService();
            _droneControlService = new DroneControlService(_networkService);

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

            Loaded += ControlPanelWindow_Loaded;
        }

        private void ControlPanelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RestoreValidationFields();
        }

        private void RestoreValidationFields()
        {
            var s = ControlPanelValidationState.Instance;
            ValidationLatitudeTextBox.Text = s.Latitude;
            ValidationLongitudeTextBox.Text = s.Longitude;
            ValidationAltitudeTextBox.Text = s.Altitude;
            ValidationYawTextBox.Text = s.Yaw;
        }

        private void PersistValidationFields()
        {
            var s = ControlPanelValidationState.Instance;
            s.Latitude = ValidationLatitudeTextBox.Text;
            s.Longitude = ValidationLongitudeTextBox.Text;
            s.Altitude = ValidationAltitudeTextBox.Text;
            s.Yaw = ValidationYawTextBox.Text;
        }

        private void SetupEventHandlers()
        {
            // Drone service events - removed event handlers since ProcessingEvents collection is removed
        }

        private void InitializeUIData()
        {
            // Initialize drone selection panel with real drones
            PopulateDroneSelectionPanel();
            
            // Subscribe to drone changes
            SubscribeToDroneChanges();
            
            // Setup validation field events to track user edits
            ValidationLatitudeTextBox.TextChanged += ValidationField_TextChanged;
            ValidationLongitudeTextBox.TextChanged += ValidationField_TextChanged;
            ValidationAltitudeTextBox.TextChanged += ValidationField_TextChanged;
            ValidationYawTextBox.TextChanged += ValidationField_TextChanged;
        }

        private void SubscribeToDroneChanges()
        {
            DeviceDataManager.DronesChanged += OnDronesChanged;
        }

        private void OnDronesChanged(List<UsbDrone> drones)
        {
            Dispatcher.Invoke(PopulateDroneSelectionPanel);
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            // Update flight data
            UpdateFlightData();

            // Update network status
            UpdateNetworkStatus();
        }

        private void UpdateFlightData()
        {
            AltitudeText.Text = $"{_droneControlService.Altitude:F1} m";
            SpeedText.Text = $"{_droneControlService.Speed:F1} m/s";
            GPSText.Text = $"{_droneControlService.Latitude:F6}, {_droneControlService.Longitude:F6}";

            // NO AUTO-FILL: User must manually enter target position
            // Validation fields remain user-editable and keep user input
        }

        private void UpdateProcessingStatistics()
        {
        }

        private void UpdateNetworkStatus()
        {
            // Network status update - network monitoring UI controls removed
        }

        // Event handlers for service events - removed (ProcessingEvents collection removed)
        private void OnDroneStatusChanged(object? sender, DroneStatusEventArgs e)
        {
            // Event handler removed
        }

        private void OnCommandExecuted(object? sender, string message)
        {
            // Event handler removed
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

        // Connection & Recording Handlers - REMOVED with Connection & Recording panel
        private async void ConnectDroneButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Connection & Recording panel
        }

        private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Connection & Recording panel
        }

        private async void CalibrateGPSButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Connection & Recording panel
        }

        // Data Processing Event Handlers - REMOVED
        private void StartProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Data Processing tab
        }

        private void StopProcessingButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Data Processing tab
        }

        private void SimulateDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Data Processing tab
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with Data Processing tab
        }

        // AI Models Event Handlers - REMOVED
        private async void InstallModelButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private void ActivateModelButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private void DeactivateModelButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private void UninstallModelButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private async void ProcessTestImageButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        {
            // Removed with AI Models tab
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SendTargetPositionButton_Click(object sender, RoutedEventArgs e)
        {
            // This handler is removed - replaced with new validation system
        }

        // Alert Validation Event Handlers
        private void GoToPositionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PersistValidationFields();

                if (string.IsNullOrWhiteSpace(_selectedDroneId))
                {
                    MessageBox.Show("Please select a drone from the Drone Selection panel at the top.", "No Drone Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseDouble(ValidationLatitudeTextBox.Text, out var targetLat) ||
                    !TryParseDouble(ValidationLongitudeTextBox.Text, out var targetLon))
                {
                    MessageBox.Show("Please enter valid Latitude and Longitude values in Target Position.", "Invalid Coordinates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseDouble(ValidationAltitudeTextBox.Text, out var targetAlt))
                {
                    MessageBox.Show("Please enter a valid Altitude value in Target Position.", "Invalid Altitude", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseDouble(ValidationYawTextBox.Text, out var targetYaw))
                {
                    MessageBox.Show("Please enter a valid Yaw value in Target Position.", "Invalid Yaw", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var curLat = _droneControlService.Latitude;
                var curLon = _droneControlService.Longitude;
                var curAlt = _droneControlService.Altitude;
                InitialLatitudeText.Text = curLat.ToString("F6");
                InitialLongitudeText.Text = curLon.ToString("F6");
                InitialAltitudeText.Text = curAlt.ToString("F1") + " m";

                SendDroneValidationTask(_selectedDroneId, targetLat, targetLon, targetAlt, targetYaw, "move");

                ValidationStatusText.Text = $"Go to Position sent ({targetLat:F5}, {targetLon:F5})";
                ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.LimeGreen);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopValidationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ValidationStatusText.Text = "Validation stopped";
                ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                GoToPositionButton.Content = "Go to Position";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidationReturnToBaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_selectedDroneId))
                {
                    MessageBox.Show("Please select a drone from the Drone Selection panel at the top.", "No Drone Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var (baseLat, baseLon, baseAlt, baseYaw) = DroneBaseConfig.Load();
                if (baseLat == 0 && baseLon == 0 && baseAlt == 0 && baseYaw == 0)
                {
                    MessageBox.Show(
                        "Set DroneBase (Latitude, Longitude, Altitude, Yaw) in appsettings.json next to the application executable, then restart or reopen the control panel.",
                        "Base location not configured",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SendDroneReturnToBaseTask(_selectedDroneId, baseLat, baseLon, baseAlt, baseYaw);
                ValidationStatusText.Text = "Return to Base command sent (configured home)";
                ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.DeepSkyBlue);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Return to Base", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendDroneValidationTask(string droneId, double latitude, double longitude, double altitude, double yaw, string action)
        {
            try
            {
                var payload = new
                {
                    type = "drone_task",
                    data = new
                    {
                        drone_id = droneId,
                        pos = new[] { latitude, longitude, altitude, yaw },
                        action = action,
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                };

                string jsonMessage = JsonConvert.SerializeObject(payload);

                if (ApiService.Instance != null)
                {
                    ApiService.Instance.SendMessage(jsonMessage);
                }

                System.Diagnostics.Debug.WriteLine($"[DroneTask] Sent {action}: {jsonMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DroneTask Error] {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Sends the drone to the predefined base from appsettings.json. Separate from Go to Position (user fields) and from Stop Validation.
        /// </summary>
        private void SendDroneReturnToBaseTask(string droneId, double latitude, double longitude, double altitude, double yaw)
        {
            try
            {
                var payload = new
                {
                    type = "drone_task",
                    data = new
                    {
                        drone_id = droneId,
                        pos = new[] { latitude, longitude, altitude, yaw },
                        action = "return_base",
                        timestamp = DateTime.UtcNow.ToString("o"),
                    }
                };

                var jsonMessage = JsonConvert.SerializeObject(payload);
                if (ApiService.Instance != null)
                {
                    ApiService.Instance.SendMessage(jsonMessage);
                }

                System.Diagnostics.Debug.WriteLine($"[ReturnToBase] Sent return_base: {jsonMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReturnToBase Error] {ex.Message}");
                throw;
            }
        }

        private static bool TryParseDouble(string? text, out double value)
        {
            value = 0;
            var raw = (text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                   double.TryParse(raw, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        protected override void OnClosed(EventArgs e)
        {
            PersistValidationFields();
            _uiUpdateTimer?.Stop();
            DeviceDataManager.DronesChanged -= OnDronesChanged;
            _droneControlService?.Dispose();
            base.OnClosed(e);
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        // =============== Drone Selection Panel Methods ===============
        private void PopulateDroneSelectionPanel()
        {
            DroneSelectionPanel.Children.Clear();

            try
            {
                var drones = DeviceDataManager.GetAllDrones();

                if (drones == null || drones.Count == 0)
                {
                    var noDronesText = new TextBlock
                    {
                        Text = "No drones connected. Add drones from Connected Drones page.",
                        Foreground = new SolidColorBrush(Colors.Orange),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(10, 0, 0, 0)
                    };
                    DroneSelectionPanel.Children.Add(noDronesText);
                    _selectedDroneId = "";
                    _selectedDroneButton = null;
                    return;
                }

                Button? firstButton = null;
                UsbDrone? firstDrone = null;
                Button? matchButton = null;
                UsbDrone? matchDrone = null;

                foreach (var drone in drones)
                {
                    var droneKey = string.IsNullOrWhiteSpace(drone.DeviceId) ? (drone.Name ?? "") : drone.DeviceId;
                    if (string.IsNullOrWhiteSpace(droneKey))
                    {
                        continue;
                    }

                    var button = new Button
                    {
                        Content = drone.Name ?? droneKey,
                        Style = (Style)FindResource("DroneChipButtonStyle"),
                        Margin = new Thickness(5),
                        Tag = droneKey,
                        ToolTip = $"Status: {drone.Status}\nBattery: {drone.BatteryLevel}%"
                    };

                    if (drone.IsConnected)
                    {
                        button.Background = new SolidColorBrush(Color.FromRgb(30, 64, 175));
                        button.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                        button.Opacity = 1.0;
                    }
                    else
                    {
                        button.Background = new SolidColorBrush(Color.FromRgb(55, 65, 81));
                        button.BorderBrush = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                        button.Opacity = 0.85;
                    }

                    var capturedDrone = drone;
                    var capturedKey = droneKey;
                    button.Click += (s, e) => SelectDrone(button, capturedKey, capturedDrone.Name ?? capturedKey, capturedDrone.BatteryLevel, capturedDrone.IsConnected, capturedDrone.Status);
                    DroneSelectionPanel.Children.Add(button);

                    if (firstButton == null)
                    {
                        firstButton = button;
                        firstDrone = drone;
                    }

                    if (!string.IsNullOrWhiteSpace(_selectedDroneId) &&
                        string.Equals(droneKey, _selectedDroneId, StringComparison.OrdinalIgnoreCase))
                    {
                        matchButton = button;
                        matchDrone = drone;
                    }
                }

                if (matchButton != null && matchDrone != null)
                {
                    var k = string.IsNullOrWhiteSpace(matchDrone.DeviceId) ? (matchDrone.Name ?? "") : matchDrone.DeviceId;
                    SelectDrone(matchButton, k, matchDrone.Name ?? k, matchDrone.BatteryLevel, matchDrone.IsConnected, matchDrone.Status);
                }
                else if (firstButton != null && firstDrone != null)
                {
                    var k = string.IsNullOrWhiteSpace(firstDrone.DeviceId) ? (firstDrone.Name ?? "") : firstDrone.DeviceId;
                    SelectDrone(firstButton, k, firstDrone.Name ?? k, firstDrone.BatteryLevel, firstDrone.IsConnected, firstDrone.Status);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error populating drone list: {ex.Message}");
            }
        }

        private void SelectDrone(Button sourceButton, string droneId, string droneName, int batteryLevel, bool isConnected, string status)
        {
            _selectedDroneId = droneId;
            _isDroneConnected = isConnected;

            if (_selectedDroneButton != null)
            {
                _selectedDroneButton.BorderThickness = new Thickness(1);
            }

            _selectedDroneButton = sourceButton;
            _selectedDroneButton.BorderBrush = new SolidColorBrush(Color.FromRgb(34, 197, 94));
            _selectedDroneButton.BorderThickness = new Thickness(2);
            
            // Update status display
            var statusText = FindName("SelectedDroneStatusText") as TextBlock;
            if (statusText != null)
            {
                statusText.Text = $"Selected: {droneName} ({status})";
                statusText.Foreground = isConnected ? new SolidColorBrush(Colors.Lime) : new SolidColorBrush(Colors.Orange);
            }

            // Update drone info display
            var infoText = FindName("SelectedDroneInfoText") as TextBlock;
            if (infoText != null)
            {
                infoText.Text = isConnected
                    ? $"Battery: {batteryLevel}% | Ready for validation"
                    : $"Battery: {batteryLevel}% | Drone is disconnected (commands may fail)";
            }
        }

        private void ValidationField_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            PersistValidationFields();
        }

        public void RefreshDroneList()
        {
            // Method to refresh drone list when called from other pages
            PopulateDroneSelectionPanel();
        }
    }
}
