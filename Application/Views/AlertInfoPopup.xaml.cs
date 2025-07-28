using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DroneSurveillanceSystem.Views
{
    public partial class AlertInfoPopup : Window
    {
        private readonly AlertData _alertData;
        public AlertInfoPopup(AlertData alertData)
        {
            InitializeComponent();
            _alertData = alertData;
            LoadAlertDetails();
        }

        private void LoadAlertDetails()
        {
            try
            {
                // Set alert title
                AlertTitleText.Text = _alertData.Alert ?? "Unknown Alert";
                
                // Alert description
                AlertDescriptionText.Text = _alertData.Alert ?? "No description available";
                
                // Drone information
                var droneIdValue = _alertData.DroneId;
                if (string.IsNullOrEmpty(droneIdValue))
                {
                    droneIdValue = "Unknown";
                }
                DroneIdText.Text = $"Drone ID: {droneIdValue}";
                
                // Location information
                if (_alertData.AlertLocation != null)
                {
                    LocationText.Text = $"Latitude: {_alertData.AlertLocation.Item1:F6}\nLongitude: {_alertData.AlertLocation.Item2:F6}\nAltitude: {_alertData.AlertLocation.Item3:F1}m";
                }
                else
                {
                    LocationText.Text = "Location data not available";
                }
                
                // Score information
                ScoreText.Text = $"{_alertData.Score:F2}%";
                
                // Timestamp information
                if (!string.IsNullOrEmpty(_alertData.Timestamp))
                {
                    if (DateTime.TryParse(_alertData.Timestamp, out var parsedTime))
                    {
                        TimestampText.Text = parsedTime.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    }
                    else
                    {
                        TimestampText.Text = _alertData.Timestamp;
                    }
                }
                else
                {
                    TimestampText.Text = "Timestamp not available";
                }
                
                // RL Response status
                RLStatusText.Text = _alertData.RLResponsed == 1 ? "✅ Sent" : "❌ Not Sent";
                
                // Image status and handling
                if (_alertData.ImageReceived == 1 && !string.IsNullOrEmpty(_alertData.Image))
                {
                    ImageStatusText.Text = "✅ Image Available";
                    
                    try
                    {
                        // Try to load the image
                        var imageUri = new Uri(_alertData.Image, UriKind.RelativeOrAbsolute);
                        AlertImage.Source = new BitmapImage(imageUri);
                        
                        // Show image section and view full image button
                        ImageSection.Visibility = Visibility.Visible;
                        ViewFullImageButton.Visibility = Visibility.Visible;
                    }
                    catch (Exception ex)
                    {
                        ImageStatusText.Text = "❌ Image failed to load";
                        System.Diagnostics.Debug.WriteLine($"Error loading image: {ex.Message}");
                        
                        // Hide image section and view full image button
                        ImageSection.Visibility = Visibility.Collapsed;
                        ViewFullImageButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    ImageStatusText.Text = "❌ No image available";
                    
                    // Hide image section and view full image button
                    ImageSection.Visibility = Visibility.Collapsed;
                    ViewFullImageButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading alert details: {ex.Message}");
                
                // Set fallback values
                AlertTitleText.Text = "Error Loading Alert";
                AlertDescriptionText.Text = "Failed to load alert details";
                DroneIdText.Text = "Error";
                LocationText.Text = "Error";
                ScoreText.Text = "Error";
                TimestampText.Text = "Error";
                RLStatusText.Text = "Error";
                ImageStatusText.Text = "Error";
                
                // Hide image section
                ImageSection.Visibility = Visibility.Collapsed;
                ViewFullImageButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ViewFullImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_alertData.ImageReceived == 1 && !string.IsNullOrEmpty(_alertData.Image))
            {
                try
        {
            var imgViewer = new ImgViewer();
            imgViewer.Owner = this;
            imgViewer.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening image viewer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    // AlertData class for passing alert info
    public class AlertData
    {
        public string? Alert { get; set; }
        public string? DroneId { get; set; }
        public Tuple<double, double, double>? AlertLocation { get; set; }
        public string? Image { get; set; }
        public int ImageReceived { get; set; }
        public int RLResponsed { get; set; }
        public double Score { get; set; }
        public string? Timestamp { get; set; }
    }
}
