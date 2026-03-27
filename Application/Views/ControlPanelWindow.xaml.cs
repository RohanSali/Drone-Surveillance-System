using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
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

        // Validation state tracking
        private bool _isValidationActive = false;
        private ValidationState _validationState = ValidationState.Idle;
        private double _initialLatitude = 0.0;
        private double _initialLongitude = 0.0;
        private double _initialAltitude = 0.0;

        // Validation states enum
        private enum ValidationState
        {
            Idle,
            Moving,
            Hovering,
            Returning
        }

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
        }

        private void SetupEventHandlers()
        {
            // Drone service events - removed event handlers since ProcessingEvents collection is removed
        }

        private void InitializeUIData()
        {
            // Initialize with drone default data
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

            // Update validation panel with current drone position (auto-fill on first load)
            if (!_isValidationActive)
            {
                ValidationLatitudeTextBox.Text = _droneControlService.Latitude.ToString("F6");
                ValidationLongitudeTextBox.Text = _droneControlService.Longitude.ToString("F6");
                ValidationAltitudeTextBox.Text = _droneControlService.Altitude.ToString("F1");
            }
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
                // Parse input values
                var droneId = (ValidationDroneIdTextBox.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(droneId))
                {
                    MessageBox.Show("Please enter a Drone ID.", "Missing Drone ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseDouble(ValidationLatitudeTextBox.Text, out var targetLat) ||
                    !TryParseDouble(ValidationLongitudeTextBox.Text, out var targetLon))
                {
                    MessageBox.Show("Please enter valid Latitude and Longitude values.", "Invalid Coordinates", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TryParseDouble(ValidationAltitudeTextBox.Text, out var targetAlt))
                {
                    MessageBox.Show("Please enter a valid Altitude value.", "Invalid Altitude", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // First click: Moving to alert position for validation
                if (!_isValidationActive)
                {
                    // Capture current position as initial position
                    _initialLatitude = _droneControlService.Latitude;
                    _initialLongitude = _droneControlService.Longitude;
                    _initialAltitude = _droneControlService.Altitude;

                    // Update initial position display
                    InitialLatitudeText.Text = _initialLatitude.ToString("F6");
                    InitialLongitudeText.Text = _initialLongitude.ToString("F6");
                    InitialAltitudeText.Text = _initialAltitude.ToString("F1") + " m";

                    // Send move command via WebSocket
                    SendDroneValidationTask(droneId, targetLat, targetLon, targetAlt, "move");

                    _isValidationActive = true;
                    _validationState = ValidationState.Moving;

                    // Update UI
                    GoToPositionButton.Content = "Return to Base";
                    ValidationStatusText.Text = "Moving to Alert Location";
                    ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.Yellow);

                    MessageBox.Show($"Drone moving to alert position: {targetLat:F6}, {targetLon:F6}, {targetAlt:F1}m", "Validation Started", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Second click: Returning to initial position
                    SendDroneValidationTask(droneId, _initialLatitude, _initialLongitude, _initialAltitude, "move");

                    _validationState = ValidationState.Returning;

                    // Update UI
                    GoToPositionButton.Content = "Go to Position";
                    _isValidationActive = false;
                    ValidationStatusText.Text = "Returning to Base";
                    ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.Cyan);

                    MessageBox.Show($"Drone returning to initial position: {_initialLatitude:F6}, {_initialLongitude:F6}, {_initialAltitude:F1}m", "Return to Base", MessageBoxButton.OK, MessageBoxImage.Information);
                }
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
                var droneId = (ValidationDroneIdTextBox.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(droneId))
                {
                    MessageBox.Show("Please enter a Drone ID.", "Missing Drone ID", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Send hover command at current position
                var currentLat = _droneControlService.Latitude;
                var currentLon = _droneControlService.Longitude;
                var currentAlt = _droneControlService.Altitude;

                SendDroneValidationTask(droneId, currentLat, currentLon, currentAlt, "hover");

                _validationState = ValidationState.Hovering;

                // Update UI
                ValidationStatusText.Text = "Hovering (Validation)";
                ValidationStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                GoToPositionButton.IsEnabled = true;

                MessageBox.Show($"Drone hovering at position for validation: {currentLat:F6}, {currentLon:F6}, {currentAlt:F1}m", "Validation Paused", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SendDroneValidationTask(string droneId, double latitude, double longitude, double altitude, string action)
        {
            try
            {
                var payload = new
                {
                    type = "drone_task",
                    data = new
                    {
                        drone_id = droneId,
                        latitude = latitude,
                        longitude = longitude,
                        altitude = altitude,
                        action = action,
                        timestamp = DateTime.UtcNow.ToString("o"),
                        validation_type = "alert"
                    }
                };

                string jsonMessage = JsonConvert.SerializeObject(payload);
                
                // Send via WebSocket
                if (ApiService.Instance != null)
                {
                    ApiService.Instance.SendMessage(jsonMessage);
                }

                // Log the action
                System.Diagnostics.Debug.WriteLine($"[Validation] Sent {action} command: {jsonMessage}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Validation Error] {ex.Message}");
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
            _uiUpdateTimer?.Stop();
            _droneControlService?.Dispose();
            base.OnClosed(e);
        }

        private void TabControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
