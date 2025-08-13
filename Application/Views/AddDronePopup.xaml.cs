using System;
using System.Windows;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class AddDronePopup : Window
    {
        public UsbDrone? NewDrone { get; private set; }

        public AddDronePopup()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(DroneIdTextBox.Text) ||
                string.IsNullOrWhiteSpace(DroneNameTextBox.Text) ||
                string.IsNullOrWhiteSpace(BluetoothMacTextBox.Text) ||
                string.IsNullOrWhiteSpace(IpAddressTextBox.Text) ||
                string.IsNullOrWhiteSpace(SimTypeTextBox.Text) ||
                string.IsNullOrWhiteSpace(LocationTextBox.Text))
            {
                MessageBox.Show("Please fill in all fields.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create new drone with the form data
            NewDrone = new UsbDrone
            {
                Name = DroneNameTextBox.Text.Trim(),
                DeviceId = DroneIdTextBox.Text.Trim(),
                UsbPort = "COM" + new Random().Next(1, 20), // Generate random COM port
                DroneType = "Surveillance",
                FirmwareVersion = "v2.1.0",
                BatteryLevel = new Random().Next(60, 100),
                Status = "Connected - Dummy",
                // Add new properties (we'll need to extend the UsbDrone class)
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
