using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DroneSurveillanceSystem.Services;
using Microsoft.Win32;

namespace DroneSurveillanceSystem.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly SurveillanceService _surveillanceService;

        public SettingsWindow()
        {
            InitializeComponent();
            _surveillanceService = new SurveillanceService();
            LoadCurrentSettings();
            SetupEventHandlers();
        }

        private void LoadCurrentSettings()
        {
            // Set data path label
            string dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance");
            DataPathLabel.Text = dataPath;

            // Load saved settings or use defaults
            // In a real application, these would be loaded from a settings file
            AiDetectionToggle.IsChecked = true;
            VoiceAlertsToggle.IsChecked = false;
            SensitivitySlider.Value = 5;
            DefaultCameraCombo.SelectedIndex = 3; // 360° View
            FrameRateCombo.SelectedIndex = 2; // 30 FPS
            DroneIdTextBox.Text = "Drone-001";
            AltitudeSlider.Value = 50;
            AutoSaveToggle.IsChecked = true;
            RetentionCombo.SelectedIndex = 1; // 30 days

            UpdateSliderLabels();
        }

        private void SetupEventHandlers()
        {
            SensitivitySlider.ValueChanged += SensitivitySlider_ValueChanged;
            AltitudeSlider.ValueChanged += AltitudeSlider_ValueChanged;
        }

        private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SensitivityValue != null)
            {
                string level = e.NewValue switch
                {
                    <= 2 => "Very Low",
                    <= 4 => "Low",
                    <= 6 => "Medium",
                    <= 8 => "High",
                    _ => "Very High"
                };
                SensitivityValue.Text = $"{level} ({(int)e.NewValue})";
            }
        }

        private void AltitudeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (AltitudeValue != null)
            {
                AltitudeValue.Text = $"{(int)e.NewValue} meters";
            }
        }

        private void UpdateSliderLabels()
        {
            SensitivitySlider_ValueChanged(SensitivitySlider, new RoutedPropertyChangedEventArgs<double>(0, SensitivitySlider.Value));
            AltitudeSlider_ValueChanged(AltitudeSlider, new RoutedPropertyChangedEventArgs<double>(0, AltitudeSlider.Value));
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
                // Reset all controls to default values
                AiDetectionToggle.IsChecked = true;
                VoiceAlertsToggle.IsChecked = false;
                SensitivitySlider.Value = 5;
                DefaultCameraCombo.SelectedIndex = 3; // 360° View
                FrameRateCombo.SelectedIndex = 2; // 30 FPS
                DroneIdTextBox.Text = "Drone-001";
                AltitudeSlider.Value = 50;
                AutoSaveToggle.IsChecked = true;
                RetentionCombo.SelectedIndex = 1; // 30 days

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
                if (string.IsNullOrWhiteSpace(DroneIdTextBox.Text))
                {
                    MessageBox.Show("Drone ID cannot be empty.", 
                                  "Validation Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Warning);
                    return;
                }

                // In a real application, save settings to configuration file
                SaveSettingsToFile();

                MessageBox.Show("Settings saved successfully!", 
                              "Settings Saved", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);

                DialogResult = true;
                Close();
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
                    AiDetectionEnabled = AiDetectionToggle.IsChecked ?? true,
                    VoiceAlertsEnabled = VoiceAlertsToggle.IsChecked ?? false,
                    DetectionSensitivity = (int)SensitivitySlider.Value,
                    DefaultCamera = ((ComboBoxItem)DefaultCameraCombo.SelectedItem)?.Content?.ToString() ?? "360° View",
                    FrameRate = ((ComboBoxItem)FrameRateCombo.SelectedItem)?.Content?.ToString() ?? "30 FPS",
                    DroneId = DroneIdTextBox.Text,
                    FlightAltitude = (int)AltitudeSlider.Value,
                    AutoSave = AutoSaveToggle.IsChecked ?? true,
                    LogRetention = ((ComboBoxItem)RetentionCombo.SelectedItem)?.Content?.ToString() ?? "30 days",
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Additional initialization if needed
        }
    }
}
