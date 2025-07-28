using System;
using System.Windows;

namespace DroneSurveillanceSystem.Views
{
    public partial class DroneDetailsPage : Window
    {
        private string droneName = string.Empty;

        public DroneDetailsPage()
        {
            InitializeComponent();
        }

        public DroneDetailsPage(string droneName)
        {
            InitializeComponent();
            this.droneName = droneName;
            InitializeDroneDetails();
        }

        private void InitializeDroneDetails()
        {
            // Set drone name in UI elements
            DroneNameHeader.Text = $"{droneName} - Details";
            DroneNameDisplay.Text = droneName;
            
            // Set USB port info based on drone name
            switch (droneName)
            {
                case "Drone_Alpha_1":
                    UsbPortInfo.Text = "USB Port: COM3";
                    break;
                case "Drone_Beta_2":
                    UsbPortInfo.Text = "USB Port: COM5";
                    break;
                case "Drone_Gamma_3":
                    UsbPortInfo.Text = "USB Port: COM7";
                    break;
                case "Drone_Theta_4":
                    UsbPortInfo.Text = "USB Port: COM9";
                    break;
                default:
                    UsbPortInfo.Text = "USB Port: Unknown";
                    break;
            }
        }

        private void BackToDronesListButton_Click(object sender, RoutedEventArgs e)
        {
            // Navigate back to Connected Drones Page
            ConnectedDronesPage connectedDronesPage = new ConnectedDronesPage();
            connectedDronesPage.Show();
            this.Close();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            // Show connection options (Fetch and Install)
            ConnectionOptionsPanel.Visibility = Visibility.Visible;
            
            // Update footer message
            MessageBox.Show($"Connection established with {droneName}. Choose your action below.", 
                          "Connection Successful", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            // Confirm removal
            MessageBoxResult result = MessageBox.Show(
                $"Are you sure you want to remove {droneName} from the connected devices?", 
                "Confirm Removal", 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show($"{droneName} has been safely removed from USB connection.", 
                              "Drone Removed", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
                
                // Navigate back to Connected Drones Page
                ConnectedDronesPage connectedDronesPage = new ConnectedDronesPage();
                connectedDronesPage.Show();
                this.Close();
            }
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            // Open module selection popup
            ModuleSelectionPopup moduleSelectionPopup = new ModuleSelectionPopup(droneName);
            moduleSelectionPopup.ShowDialog();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            // Show install functionality
            MessageBox.Show($"Installing pre-configured modules to {droneName}...\n\nInstallation completed successfully!", 
                          "Module Installation", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Information);
        }
    }
}
