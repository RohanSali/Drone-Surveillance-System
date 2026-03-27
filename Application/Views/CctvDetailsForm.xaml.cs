using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class CctvDetailsForm : Window
    {
        private readonly UsbCctv _cctv;
        // Add 'new' keyword to explicitly hide the base member
        public new bool? DialogResult { get; set; }
        public CctvConfiguration? Configuration { get; private set; }

        public CctvDetailsForm(UsbCctv cctv)
        {
            InitializeComponent();
            _cctv = cctv;
            LoadExistingData();
        }

        private void LoadExistingData()
        {
            // Pre-populate with existing CCTV data if available
            Title = $"CCTV Configuration - {_cctv.Name}";
            
            // You can load from _cctv properties or set defaults
            CameraIpTextBox.Text = _cctv.DeviceId.Contains("001") ? "192.168.1.101" : 
                                  _cctv.DeviceId.Contains("002") ? "192.168.1.102" : 
                                  "192.168.1.103";
            
            LocationTextBox.Text = _cctv.Name.Contains("A") ? "Main Entrance" :
                                  _cctv.Name.Contains("B") ? "Parking Area" :
                                  "Side Gate";
            
            ModelTextBox.Text = "HD-" + _cctv.DeviceId.Substring(_cctv.DeviceId.Length - 3) + " Pro";
            
            // Set resolution and FPS from existing data
            var resolutionItem = ResolutionComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString().Contains(_cctv.Resolution));
            if (resolutionItem != null)
                ResolutionComboBox.SelectedItem = resolutionItem;
            
            var fpsItem = FpsComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Content.ToString().Contains(_cctv.FrameRate.ToString()));
            if (fpsItem != null)
                FpsComboBox.SelectedItem = fpsItem;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                Configuration = new CctvConfiguration
                {
                    CameraIp = CameraIpTextBox.Text.Trim(),
                    Location = LocationTextBox.Text.Trim(),
                    Model = ModelTextBox.Text.Trim(),
                    TimestampFormat = ((ComboBoxItem)TimestampComboBox.SelectedItem).Content.ToString(),
                    FramesPerSecond = int.Parse(((ComboBoxItem)FpsComboBox.SelectedItem).Content.ToString().Split(' ')[0]),
                    Resolution = ((ComboBoxItem)ResolutionComboBox.SelectedItem).Content.ToString(),
                    NightVisionEnabled = NightVisionCheckBox.IsChecked ?? false,
                    MotionDetectionEnabled = MotionDetectionCheckBox.IsChecked ?? false,
                    AudioRecordingEnabled = AudioRecordingCheckBox.IsChecked ?? false,
                    ConfigurationDate = DateTime.Now
                };

                DialogResult = true;
                ShowSuccessMessage();
                this.Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private bool ValidateInput()
        {
            // Validate Camera IP
            if (string.IsNullOrWhiteSpace(CameraIpTextBox.Text))
            {
                ShowError("Camera IP Address is required.");
                CameraIpTextBox.Focus();
                return false;
            }

            if (!IsValidIP(CameraIpTextBox.Text.Trim()))
            {
                ShowError("Please enter a valid IP address (e.g., 192.168.1.100).");
                CameraIpTextBox.Focus();
                return false;
            }

            // Validate Location
            if (string.IsNullOrWhiteSpace(LocationTextBox.Text))
            {
                ShowError("Location is required.");
                LocationTextBox.Focus();
                return false;
            }

            // Validate Model
            if (string.IsNullOrWhiteSpace(ModelTextBox.Text))
            {
                ShowError("Camera Model is required.");
                ModelTextBox.Focus();
                return false;
            }

            // Validate ComboBox selections
            if (TimestampComboBox.SelectedItem == null)
            {
                ShowError("Please select a timestamp format.");
                TimestampComboBox.Focus();
                return false;
            }

            if (FpsComboBox.SelectedItem == null)
            {
                ShowError("Please select frames per second.");
                FpsComboBox.Focus();
                return false;
            }

            if (ResolutionComboBox.SelectedItem == null)
            {
                ShowError("Please select a resolution.");
                ResolutionComboBox.Focus();
                return false;
            }

            return true;
        }

        private bool IsValidIP(string ip)
        {
            string pattern = @"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            return Regex.IsMatch(ip, pattern);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Validation Error", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void ShowSuccessMessage()
        {
            MessageBox.Show(
                $"CCTV configuration saved successfully!\n\n" +
                $"Camera: {Configuration.Model}\n" +
                $"IP: {Configuration.CameraIp}\n" +
                $"Location: {Configuration.Location}\n" +
                $"Resolution: {Configuration.Resolution}\n" +
                $"FPS: {Configuration.FramesPerSecond}\n\n" +
                $"Configuration will be applied to {_cctv.Name}.",
                "Configuration Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }

    public class CctvConfiguration
    {
        public string CameraIp { get; set; } = "";
        public string Location { get; set; } = "";
        public string Model { get; set; } = "";
        public string TimestampFormat { get; set; } = "";
        public int FramesPerSecond { get; set; }
        public string Resolution { get; set; } = "";
        public bool NightVisionEnabled { get; set; }
        public bool MotionDetectionEnabled { get; set; }
        public bool AudioRecordingEnabled { get; set; }
        public DateTime ConfigurationDate { get; set; }

        public string GetSummary()
        {
            return $"IP: {CameraIp} | Location: {Location} | {Resolution} @ {FramesPerSecond}FPS";
        }
    }
}
