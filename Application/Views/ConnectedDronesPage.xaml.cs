using System;
using System.Windows;
using System.Windows.Input;

namespace DroneSurveillanceSystem.Views
{
    public partial class ConnectedDronesPage : Window
    {
        public ConnectedDronesPage()
        {
            InitializeComponent();
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

        private void DronesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (DronesList.SelectedItem != null)
            {
                // Get the selected drone name from the UI
                string selectedDroneName = GetSelectedDroneName();
                
                // Open drone details page
                DroneDetailsPage droneDetailsPage = new DroneDetailsPage(selectedDroneName);
                droneDetailsPage.Show();
                this.Close();
            }
        }

        private string GetSelectedDroneName()
        {
            // Extract drone name from selected item
            var selectedIndex = DronesList.SelectedIndex;
            switch (selectedIndex)
            {
                case 0: return "Drone_Alpha_1";
                case 1: return "Drone_Beta_2";
                case 2: return "Drone_Gamma_3";
                case 3: return "Drone_Theta_4";
                default: return "Unknown_Drone";
            }
        }
    }
}
