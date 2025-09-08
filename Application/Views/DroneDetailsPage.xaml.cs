using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class DroneDetailsPage : Window
    {
        private UsbDrone? _selectedDrone;
        private readonly UsbDroneService _usbDroneService;

        public DroneDetailsPage()
        {
            InitializeComponent();
            _usbDroneService = new UsbDroneService();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
        }

        public DroneDetailsPage(string droneName)
        {
            InitializeComponent();
            _usbDroneService = new UsbDroneService();
            _selectedDrone = _usbDroneService.GetDroneByName(droneName);
            InitializeDroneDetails();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
        }

        public DroneDetailsPage(UsbDrone drone)
        {
            InitializeComponent();
            _selectedDrone = drone;
            _usbDroneService = new UsbDroneService();
            InitializeDroneDetails();
            
            // Ensure window opens in full screen
            this.WindowState = WindowState.Maximized;
        }

        private void InitializeDroneDetails()
        {
            if (_selectedDrone == null) return;

            // Set DataContext for binding
            this.DataContext = _selectedDrone;

            // Set drone name in UI elements
            DroneNameHeader.Text = $"{_selectedDrone.Name} - Details";
            DroneNameDisplay.Text = _selectedDrone.Name;
            
            // Set USB port info
            UsbPortInfo.Text = $"USB Port: {_selectedDrone.UsbPort}";
        }

        private void BackToDronesListButton_Click(object sender, RoutedEventArgs e)
        {
            // Show Connected Drones inside MainWindow overlay
            var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (main != null)
            {
                main.ShowOverlay(new ConnectedDronesPage());
            }
            this.Close();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrone == null) return;

            try
            {
                // Simulate connection success for demo purposes
                // In a real implementation, this would use the actual service
                bool connected = true; // Simulate successful connection
                
                if (connected)
                {
                    // Update drone status locally
                    _selectedDrone.Status = "Connected - Ready for Operations";
                    
                    // Show connection options (Fetch and Install) immediately
                    ConnectionOptionsPanel.Visibility = Visibility.Visible;
                    
                    // Force UI update multiple times to ensure visibility
                    this.UpdateLayout();
                    Task.Delay(50).ContinueWith(t => this.UpdateLayout());
                    
                    // Scroll to the options panel to ensure it's visible
                    if (ConnectionOptionsPanel.Parent is FrameworkElement parent)
                    {
                        ConnectionOptionsPanel.BringIntoView();
                    }
                    
                    // Update footer message after ensuring UI is updated
                    Task.Delay(100).ContinueWith(t => MessageBox.Show($"Connection established with {_selectedDrone.Name}. Choose your action below.", 
                                  "Connection Successful", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information));
                }
                else
                {
                    MessageBox.Show($"Failed to connect to {_selectedDrone.Name}.", 
                                  "Connection Failed", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to drone: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrone == null) return;

            // Confirm removal
            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to remove {_selectedDrone.Name} from the connected devices?", 
                "Confirm Removal", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Simulate successful removal for demo purposes
                    bool disconnected = true; // Simulate successful disconnection
                    
                    if (disconnected)
                    {
                        MessageBox.Show($"{_selectedDrone.Name} has been safely removed from USB connection.", 
                                      "Drone Removed", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Failed to remove {_selectedDrone.Name}.", 
                                      "Removal Failed", 
                                      MessageBoxButton.OK, 
                                      MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error removing drone: {ex.Message}", 
                                  "Error", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Error);
                }
                
                // Show Connected Drones inside MainWindow overlay
                var main = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (main != null)
                {
                    main.ShowOverlay(new ConnectedDronesPage());
                }
                this.Close();
            }
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrone == null) return;

            try
            {
                // Simulate data fetching for demo purposes
                bool fetched = true; // Simulate successful fetch
                
                if (fetched)
                {
                    // Show success message only
                    MessageBox.Show($"Data fetched successfully from {_selectedDrone.Name}.\n\nAvailable modules and data have been retrieved.", 
                                  "Data Fetch Successful", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to fetch data from {_selectedDrone.Name}.", 
                                  "Fetch Failed", 
                                  MessageBoxButton.OK, 
                                  MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching data: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDrone == null) return;

            try
            {
                // Open module selection popup
                ModuleSelectionPopup moduleSelectionPopup = new ModuleSelectionPopup(_selectedDrone.Name);
                moduleSelectionPopup.Owner = this; // Set this window as owner
                moduleSelectionPopup.ShowDialog(); // Show as modal dialog
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening module selection: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }
    }
}
