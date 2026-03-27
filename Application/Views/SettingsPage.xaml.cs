using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DroneSurveillanceSystem.Services;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.Reflection;
using DroneSurveillanceSystem.Services.Firebase;

namespace DroneSurveillanceSystem.Views
{
    public partial class SettingsPage : UserControl
    {
        private readonly SurveillanceService _surveillanceService;
        public ObservableCollection<UsbDrone> Drones { get; } = new ObservableCollection<UsbDrone>();
        public ObservableCollection<UsbCctv> Cctvs { get; } = new ObservableCollection<UsbCctv>();

        public SettingsPage()
        {
            InitializeComponent();
            _surveillanceService = new SurveillanceService();
            this.DataContext = this;
            LoadCurrentSettings();

            // Populate system information labels
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "";
                if (AppVersionLabel != null) AppVersionLabel.Text = version;
            }
            catch { }

            try { if (OsVersionLabel != null) OsVersionLabel.Text = Environment.OSVersion.ToString(); } catch { }
            try { if (MachineNameLabel != null) MachineNameLabel.Text = Environment.MachineName; } catch { }
            try { if (UserNameLabel != null) UserNameLabel.Text = Environment.UserName; } catch { }
            try { if (ClrVersionLabel != null) ClrVersionLabel.Text = Environment.Version.ToString(); } catch { }
            try { if (ProcessorCountLabel != null) ProcessorCountLabel.Text = Environment.ProcessorCount.ToString(); } catch { }
            try { if (CurrentDirLabel != null) CurrentDirLabel.Text = Environment.CurrentDirectory; } catch { }
        }

        private void LoadCurrentSettings()
        {
            // Set data path label
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance");
            if (DataPathLabel != null) DataPathLabel.Text = dataPath;

            // Load saved settings or use defaults (moved out of per-device controls)

            // Load devices for current user
            try
            {
                Drones.Clear();
                foreach (var d in DeviceDataManager.GetAllDrones()) Drones.Add(d);
                Cctvs.Clear();
                foreach (var c in DeviceDataManager.GetAllCctvs()) Cctvs.Add(c);

                DeviceDataManager.DronesChanged += OnDronesChanged;
                DeviceDataManager.CctvsChanged += OnCctvsChanged;
            }
            catch { }
        }

        private void OnDronesChanged(System.Collections.Generic.List<UsbDrone> drones)
        {
            Dispatcher.Invoke(() =>
            {
                Drones.Clear();
                foreach (var d in drones) Drones.Add(d);
            });
        }

        private void OnCctvsChanged(System.Collections.Generic.List<UsbCctv> cctvs)
        {
            Dispatcher.Invoke(() =>
            {
                Cctvs.Clear();
                foreach (var c in cctvs) Cctvs.Add(c);
            });
        }

        private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                    Title = "Export Surveillance Data",
                    FileName = $"surveillance_export_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string format = Path.GetExtension(saveFileDialog.FileName).ToLower() == ".csv" ? "csv" : "json";

                    // Export data from the last 30 days
                    DateTime fromDate = DateTime.Now.AddDays(-30);
                    DateTime toDate = DateTime.Now;

                    string? exportPath = await _surveillanceService.ExportDetectionDataAsync(fromDate, toDate, format);

                    if (!string.IsNullOrEmpty(exportPath))
                    {
                        // Copy to user-selected location
                        File.Copy(exportPath, saveFileDialog.FileName, true);

                        MessageBox.Show($"Data exported successfully to:\n{saveFileDialog.FileName}",
                                      "Export Complete",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Failed to export data. Please try again.",
                                      "Export Error",
                                      MessageBoxButton.OK,
                                      MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting data: {ex.Message}",
                              "Export Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to clear all surveillance logs?\nThis action cannot be undone.",
                                       "Confirm Clear Logs",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Clear log files
                    string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance");
                    string jsonLogPath = Path.Combine(dataPath, "detection_log.json");
                    string databasePath = Path.Combine(dataPath, "surveillance.db");

                    if (File.Exists(jsonLogPath))
                    {
                        File.Delete(jsonLogPath);
                    }

                    if (File.Exists(databasePath))
                    {
                        File.Delete(databasePath);
                    }

                    MessageBox.Show("All surveillance logs have been cleared.",
                                  "Logs Cleared",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error clearing logs: {ex.Message}",
                                  "Clear Error",
                                  MessageBoxButton.OK,
                                  MessageBoxImage.Error);
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all settings to default values?",
                                       "Confirm Reset",
                                       MessageBoxButton.YesNo,
                                       MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("Settings have been reset to defaults.",
                              "Settings Reset",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate settings
                // if (string.IsNullOrWhiteSpace(DroneIdTextBox.Text))
                // {
                //     MessageBox.Show("Drone ID cannot be empty.", 
                //                   "Validation Error", 
                //                   MessageBoxButton.OK, 
                //                   MessageBoxImage.Warning);
                //     return;
                // }

                // // In a real application, save settings to configuration file
                // SaveSettingsToFile();

                // MessageBox.Show("Settings saved successfully!", 
                //               "Settings Saved", 
                //               MessageBoxButton.OK, 
                //               MessageBoxImage.Information);

                // // Navigate back to main window instead of closing
                // NavigateBackToMain();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}",
                              "Save Error",
                              MessageBoxButton.OK,
                              MessageBoxImage.Error);
            }
        }

        private void SaveSettingsToFile()
        {
            try
            {
                string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance");
                if (!Directory.Exists(dataPath))
                {
                    Directory.CreateDirectory(dataPath);
                }

                string settingsPath = Path.Combine(dataPath, "settings.json");

                var settings = new
                {
                    LastUpdated = DateTime.Now
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}");
            }
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Raise the event to notify the parent (MainWindow) to close this settings page
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to sign out?", "Confirm Sign Out", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                try { await ApiService.Instance.StopWebSocketAsync(); } catch { }
                AlertManager.Instance.ClearAllAlerts();

                // Reset per-user state (guest-only auth for now)
                try { LostFindingManager.Instance.ClearAllPendingRequests(); } catch { }

                // Unlock devices for this appId before switching to guest
                var currentUser = FirebaseSession.Current;
                if (currentUser != null &&
                    !string.IsNullOrWhiteSpace(currentUser.AppClientId) &&
                    !string.IsNullOrWhiteSpace(currentUser.FirebaseIdToken))
                {
                    try
                    {
                        var config = FirebaseAuthConfig.Load();
                        using var http = new System.Net.Http.HttpClient();
                        var rtdb = new FirebaseRtdbRestClient(http, config);
                        var access = new FirebaseDeviceAccessService(rtdb);
                        await access.UnlockMappedDevicesForAppAsync(currentUser.AppClientId, currentUser.FirebaseIdToken, System.Threading.CancellationToken.None);
                    }
                    catch (Exception unlockEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Device unlock failed: {unlockEx.Message}");
                    }
                }

                DeviceDataManager.SetCurrentUser("guest");
                NetworkService.SetCurrentUser("guest");

                UserProfileService.Instance.SetGuestMode();

                try
                {
                    var auth = new FirebaseAuthService();
                    await auth.SignOutAsync();
                }
                catch { }

                try { FirebaseSession.Clear(); } catch { }

                var window = Window.GetWindow(this) as MainWindow;
                if (window != null)
                {
                    window.Hide();
                }

                var loginWindow = new LoginWindow();
                if (Application.Current != null)
                {
                    Application.Current.MainWindow = loginWindow;
                }
                loginWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign out failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Event to notify when the settings page should be closed
        public event EventHandler? CloseRequested;
    }
}