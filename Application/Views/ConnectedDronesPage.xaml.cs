using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class ConnectedDronesPage : UserControl
    {
        private readonly UsbDroneService _usbDroneService;
        private readonly UsbCctvService _usbCctvService;
        private List<UsbDrone> _connectedDrones = new List<UsbDrone>();
        private List<UsbCctv> _connectedCctvs = new List<UsbCctv>();

        public ConnectedDronesPage()
        {
            InitializeComponent();
            _usbDroneService = new UsbDroneService();
            _usbCctvService = new UsbCctvService();
            _usbDroneService.DronesListChanged += OnDronesListChanged;
            _usbCctvService.CctvListChanged += OnCctvListChanged;
            
            // Subscribe to persistent data changes
            DeviceDataManager.DronesChanged += OnPersistentDronesChanged;
            DeviceDataManager.CctvsChanged += OnPersistentCctvsChanged;
            
            LoadConnectedDrones();
            LoadConnectedCctvs();
            
            // No window sizing needed in UserControl
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LoadConnectedDrones()
        {
            try
            {
                // For a clean interface per user/session on this page, load only persistent (manually added) drones
                _connectedDrones = DeviceDataManager.GetAllDrones();
                UpdateDronesList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading connected drones: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConnectedCctvs()
        {
            try
            {
                // For a clean interface per user/session on this page, load only persistent (manually added) CCTVs
                _connectedCctvs = DeviceDataManager.GetAllCctvs();
                UpdateCctvList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading connected CCTVs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        private void OnCctvListChanged(object? sender, List<UsbCctv> cams)
        {
            Dispatcher.Invoke(() =>
            {
                _connectedCctvs = cams;
                UpdateCctvList();
            });
        }

        private void OnPersistentDronesChanged(List<UsbDrone> drones)
        {
            Dispatcher.Invoke(() =>
            {
                _connectedDrones = drones;
                UpdateDronesList();
            });
        }

        private void OnPersistentCctvsChanged(List<UsbCctv> cctvs)
        {
            Dispatcher.Invoke(() =>
            {
                _connectedCctvs = cctvs;
                UpdateCctvList();
            });
        }

        private void UpdateDronesList()
        {
            Console.WriteLine($"Updating drones list. Count: {_connectedDrones.Count}");
            DronesList.ItemsSource = null;
            DronesList.ItemsSource = _connectedDrones;
        }

        private void UpdateCctvList()
        {
            Console.WriteLine($"Updating CCTV list. Count: {_connectedCctvs.Count}");
            CctvList.ItemsSource = null;
            CctvList.ItemsSource = _connectedCctvs;
        }

        private void AddDummyDroneButton_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AddDronePopup();
            var ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            if (ownerWindow != null)
            {
                popup.Owner = ownerWindow;
            }
            
            if (popup.ShowDialog() == true && popup.NewDrone != null)
            {
                try
                {
                    // Use the persistent data manager
                    DeviceDataManager.AddDrone(popup.NewDrone);
                    
                    // Refresh the local list from the persistent manager
                    _connectedDrones = DeviceDataManager.GetAllDrones();
                    UpdateDronesList();
                    
                    MessageBox.Show($"Drone '{popup.NewDrone.Name}' added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(ex.Message, "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding drone: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void AddDummyCctvButton_Click(object sender, RoutedEventArgs e)
        {
            var popup = new AddCctvPopup();
            var ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            if (ownerWindow != null)
            {
                popup.Owner = ownerWindow;
            }
            
            if (popup.ShowDialog() == true && popup.NewCctv != null)
            {
                try
                {
                    // Use the persistent data manager
                    DeviceDataManager.AddCctv(popup.NewCctv);
                    
                    // Refresh the local list from the persistent manager
                    _connectedCctvs = DeviceDataManager.GetAllCctvs();
                    UpdateCctvList();
                    
                    MessageBox.Show($"CCTV '{popup.NewCctv.Name}' added successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(ex.Message, "Duplicate Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding CCTV: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteDrone_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UsbDrone drone)
            {
                var result = MessageBox.Show($"Are you sure you want to delete drone '{drone.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // Use the persistent data manager to permanently delete
                    if (DeviceDataManager.RemoveDrone(drone))
                    {
                        // Refresh the local list from the persistent manager
                        _connectedDrones = DeviceDataManager.GetAllDrones();
                        UpdateDronesList();
                        MessageBox.Show($"Drone '{drone.Name}' deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Failed to delete drone '{drone.Name}'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteCctv_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is UsbCctv cam)
            {
                var result = MessageBox.Show($"Are you sure you want to delete CCTV '{cam.Name}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    // Use the persistent data manager to permanently delete
                    if (DeviceDataManager.RemoveCctv(cam))
                    {
                        // Refresh the local list from the persistent manager
                        _connectedCctvs = DeviceDataManager.GetAllCctvs();
                        UpdateCctvList();
                        MessageBox.Show($"CCTV '{cam.Name}' deleted successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Failed to delete CCTV '{cam.Name}'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DronesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DronesList.SelectedItem is UsbDrone selectedDrone)
            {
                // Open drone details page with the selected drone in full screen
                DroneDetailsPage droneDetailsPage = new DroneDetailsPage(selectedDrone);
                droneDetailsPage.WindowState = WindowState.Maximized;
                droneDetailsPage.Show();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private async void CctvList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CctvList.SelectedItem is UsbCctv selectedCam)
            {
                var details = new CctvDetailsPage(selectedCam, _usbCctvService);
                details.WindowState = WindowState.Maximized;
                details.Show();
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? CloseRequested;

        public void Cleanup()
        {
            if (_usbDroneService != null)
                _usbDroneService.DronesListChanged -= OnDronesListChanged;
            if (_usbCctvService != null)
                _usbCctvService.CctvListChanged -= OnCctvListChanged;
            DeviceDataManager.DronesChanged -= OnPersistentDronesChanged;
            DeviceDataManager.CctvsChanged -= OnPersistentCctvsChanged;
        }
    }
}
