using System;
using System.Windows;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class AddCctvPopup : Window
    {
        public UsbCctv? NewCctv { get; private set; }

        public AddCctvPopup()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(DeviceIdTextBox.Text) ||
                string.IsNullOrWhiteSpace(DeviceNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(BluetoothMacTextBox.Text) ||
                string.IsNullOrWhiteSpace(IpAddressTextBox.Text) ||
                string.IsNullOrWhiteSpace(SimTypeTextBox.Text) ||
                string.IsNullOrWhiteSpace(LocationTextBox.Text))
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create new CCTV with the form data
            NewCctv = new UsbCctv
            {
                Name = DeviceNameTextBox.Text.Trim(),
                DeviceId = DeviceIdTextBox.Text.Trim(),
                UsbPort = "COM" + new Random().Next(10, 30), // Generate random COM port
                FirmwareVersion = "v1.2.0",
                Resolution = "1080p",
                FrameRate = 30,
                Status = "Connected - Dummy",
                // Add new properties (we'll need to extend the UsbCctv class)
                BluetoothMacAddress = BluetoothMacTextBox.Text.Trim(),
                IpAddress = IpAddressTextBox.Text.Trim(),
                SimType = SimTypeTextBox.Text.Trim(),
                Location = LocationTextBox.Text.Trim()
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
