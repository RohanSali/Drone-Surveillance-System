using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using DroneSurveillanceSystem.Services;
using System.Windows.Threading;
using System.ComponentModel;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.Json;
using System.Text;

namespace DroneSurveillanceSystem.Views
{
    public partial class MonitoringAlertsPage : Window, INotifyPropertyChanged
    {
        public ObservableCollection<AlertData> ActiveAlerts => AlertManager.Instance.ActiveAlerts;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MonitoringAlertsPage()
        {
            InitializeComponent();
            DataContext = this;
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ActiveAlerts));
                });
            };
        }

        private void Alert_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string alertTag)
            {
                // Find the alert by tag (assume tag is alert id)
                var alert = AlertManager.Instance.ActiveAlerts.FirstOrDefault(a => a.Timestamp == alertTag);
                if (alert != null)
                {
                    var alertPopup = new AlertInfoPopup(alert);
                    alertPopup.Owner = this;
                    alertPopup.ShowDialog();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AcknowledgeAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all active alerts
                AlertManager.Instance.ActiveAlerts.Clear();
                
                // Show success message
                MessageBox.Show("All alerts have been successfully acknowledged and cleared.", 
                              "Alerts Acknowledged", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
                
                // Update the UI
                OnPropertyChanged(nameof(ActiveAlerts));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error acknowledging alerts: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
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
                imageBytes = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                base64Image = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Prepare WebSocket message (type 'alert_image' with required fields)
            var wsMessage = new
            {
                type = "alert_image",
                data = new {
                    found = 1,
                    name = "Lost Finding",
                    drone_id = "No Drone",
                    actual_image = base64Image,
                    matched_frame = base64Image, // Use same image if no matched frame
                    location = new double[] { 0, 0, 0 },
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            };
            string json = System.Text.Json.JsonSerializer.Serialize(wsMessage);

            try
            {
                var apiService = new DroneSurveillanceSystem.Services.ApiService();
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
                    await apiService._client.SendInstant(json);
                    MessageBox.Show("Lost Finding alert image sent via WebSocket!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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

    }
}
