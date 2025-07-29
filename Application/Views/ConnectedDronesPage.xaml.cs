using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class ConnectedDronesPage : Window
    {
        private readonly UsbDroneService _usbDroneService;
        private List<UsbDrone> _connectedDrones = new List<UsbDrone>();

        public ConnectedDronesPage()
        {
            InitializeComponent();
            _usbDroneService = new UsbDroneService();
            _usbDroneService.DronesListChanged += OnDronesListChanged;
            LoadConnectedDrones();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the existing MainWindow and show it
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.Show();
                    mainWindow.Activate();
                    break;
                }
            }
            this.Close();
        }

        private async void LoadConnectedDrones()
        {
            try
            {
                _connectedDrones = await _usbDroneService.DetectUsbDronesAsync();
                UpdateDronesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading connected drones: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnDronesListChanged(object? sender, List<UsbDrone> drones)
        {
            Dispatcher.Invoke(() =>
            {
                _connectedDrones = drones;
                UpdateDronesList();
            });
        }

        private void UpdateDronesList()
        {
            DronesList.ItemsSource = _connectedDrones;
        }

        private void DronesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DronesList.SelectedItem is UsbDrone selectedDrone)
            {
                // Open drone details page with the selected drone in full screen
                DroneDetailsPage droneDetailsPage = new DroneDetailsPage(selectedDrone);
                droneDetailsPage.WindowState = WindowState.Maximized;
                droneDetailsPage.Show();
                this.Close();
            }
        }
    }
}
