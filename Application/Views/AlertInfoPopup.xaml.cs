using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DroneSurveillanceSystem.Views
{
    public partial class AlertInfoPopup : Window
    {
        private AlertData _alertData;
        public AlertInfoPopup(AlertData alertData)
        {
            InitializeComponent();
            _alertData = alertData;
            LoadAlertDetails();
        }

        public string? CurrentAlertId => _alertData.AlertId;

        /// <summary>
        /// Builds a frozen <see cref="BitmapImage"/> from alert image data (data-URI base64, raw base64, or http(s) URL).
        /// Must run on the UI thread (uses WPF imaging).
        /// </summary>
        public static BitmapImage? TryLoadBitmapFromAlertImage(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var img = raw.Trim();

            try
            {
                if (img.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    img.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var uriBmp = new BitmapImage();
                    uriBmp.BeginInit();
                    uriBmp.UriSource = new Uri(img, UriKind.Absolute);
                    uriBmp.CacheOption = BitmapCacheOption.OnLoad;
                    uriBmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    uriBmp.EndInit();
                    uriBmp.Freeze();
                    return uriBmp;
                }

                var base64 = img;
                if (img.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var commaIdx = base64.IndexOf(',');
                    if (commaIdx >= 0)
                        base64 = base64[(commaIdx + 1)..];
                }

                base64 = base64.Replace("\r", "").Replace("\n", "").Replace(" ", "");
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(base64);
                }
                catch (FormatException)
                {
                    var alt = base64.Replace('-', '+').Replace('_', '/');
                    var pad = alt.Length % 4;
                    if (pad > 0)
                        alt += new string('=', 4 - pad);
                    bytes = Convert.FromBase64String(alt);
                }

                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(bytes))
                {
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                    bmp.EndInit();
                }

                bmp.Freeze();
                return bmp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AlertImage] Decode failed: {ex.Message}");
                return null;
            }
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
                DroneIdText.Text = $"{droneIdValue}";
                
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
                
                var hasPayload = !string.IsNullOrWhiteSpace(_alertData.Image);
                if (_alertData.ImageReceived == 1 || hasPayload)
                {
                    var bmp = TryLoadBitmapFromAlertImage(_alertData.Image);
                    if (bmp != null)
                    {
                        ImageStatusText.Text = "✅ Image Available";
                        AlertImage.Source = bmp;
                        ImageSection.Visibility = Visibility.Visible;
                        ViewFullImageButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        ImageStatusText.Text = hasPayload ? "❌ Image failed to decode" : "❌ No image available";
                        ImageSection.Visibility = Visibility.Collapsed;
                        ViewFullImageButton.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    ImageStatusText.Text = "❌ No image available";
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

        public void UpdateFromServer(AlertData updated)
        {
            try
            {
                // Only update if same alert id, or if no id available update anyway
                if (!string.IsNullOrEmpty(updated.AlertId) && !string.IsNullOrEmpty(_alertData.AlertId))
                {
                    if (!string.Equals(updated.AlertId, _alertData.AlertId, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                // Merge incoming fields
                _alertData.Alert = updated.Alert ?? _alertData.Alert;
                _alertData.DroneId = updated.DroneId ?? _alertData.DroneId;
                _alertData.AlertLocation = updated.AlertLocation ?? _alertData.AlertLocation;
                _alertData.Image = string.IsNullOrEmpty(updated.Image) ? _alertData.Image : updated.Image;
                _alertData.ImageReceived = updated.ImageReceived != 0 ? updated.ImageReceived : _alertData.ImageReceived;
                _alertData.RLResponsed = updated.RLResponsed != 0 ? updated.RLResponsed : _alertData.RLResponsed;
                _alertData.Score = updated.Score != 0 ? updated.Score : _alertData.Score;
                _alertData.Timestamp = updated.Timestamp ?? _alertData.Timestamp;
                _alertData.AlertId = updated.AlertId ?? _alertData.AlertId;

                // Refresh UI
                Dispatcher.Invoke(LoadAlertDetails);
            }
            catch { }
        }

        private void ViewFullImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_alertData.Image))
                return;

            try
            {
                var bmp = TryLoadBitmapFromAlertImage(_alertData.Image);
                if (bmp == null)
                {
                    MessageBox.Show("Could not decode the alert image.", "Image", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var imgViewer = new ImgViewer(bmp);
                imgViewer.Owner = this;
                imgViewer.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening image viewer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string? AlertId { get; set; }
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
