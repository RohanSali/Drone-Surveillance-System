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
            // Prevent navigation; selection is not used for navigation anymore
            DronesList.SelectedItem = null;
        }

        private async void CctvList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // Prevent navigation; selection is not used for navigation anymore
            CctvList.SelectedItem = null;
        }

        private void DroneItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Ignore if click originated from a Button inside the item
            if (e.OriginalSource is DependencyObject dep &&
                (FindAncestor<Button>(dep) != null))
            {
                return;
            }

            if (sender is ListBoxItem item && item.DataContext is UsbDrone drone)
            {
                drone.IsExpanded = !drone.IsExpanded;
                // Force UI refresh
                DronesList.Items.Refresh();
            }
        }

        private void CctvItem_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep &&
                (FindAncestor<Button>(dep) != null))
            {
                return;
            }

            if (sender is ListBoxItem item && item.DataContext is UsbCctv cam)
            {
                cam.IsExpanded = !cam.IsExpanded;
                CctvList.Items.Refresh();
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private async void DroneConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UsbDrone drone)
            {
                var success = await _usbDroneService.ConnectToDroneAsync(drone.Name);
                if (success)
                {
                    drone.Status = "Connected - Ready for Operations";
                    DronesList.Items.Refresh();
                }
                else
                {
                    MessageBox.Show($"Failed to connect to {drone.Name}.", "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DroneFetch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UsbDrone drone)
            {
                var success = await _usbDroneService.FetchDataAsync(drone.Name);
                if (success)
                {
                    MessageBox.Show($"Data fetched successfully from {drone.Name}.", "Data Fetch Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    DronesList.Items.Refresh();
                }
                else
                {
                    MessageBox.Show($"Failed to fetch data from {drone.Name}.", "Fetch Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DroneInstall_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UsbDrone drone)
            {
                try
                {
                    ModuleSelectionPopup moduleSelectionPopup = new ModuleSelectionPopup(drone.Name);
                    var ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                    if (ownerWindow != null)
                    {
                        moduleSelectionPopup.Owner = ownerWindow;
                    }
                    moduleSelectionPopup.Show();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error opening module selection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void CctvConnect_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UsbCctv cam)
            {
                if (await _usbCctvService.ConnectAsync(cam.Name))
                {
                    cam.Status = "Connected - Ready";
                    CctvList.Items.Refresh();
                }
            }
        }

        private async void CctvFetch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UsbCctv cam)
            {
                try
                {
                    // Simulate fetch then open inline config dialog as in details page
                    await System.Threading.Tasks.Task.Delay(500);
                    var configForm = new CctvDetailsForm(cam);
                    var ownerWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
                    if (ownerWindow != null)
                    {
                        configForm.Owner = ownerWindow;
                    }
                    configForm.ShowDialog();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error fetching CCTV details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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
