using System;
using System.Windows;

namespace DroneSurveillanceSystem.Views
{
    public partial class NetworkMonitoringPage : Window
    {
        public NetworkMonitoringPage()
        {
            InitializeComponent();
        }

        private void Network1Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 1", "Primary Surveillance Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void Network2Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 2", "Secondary Patrol Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void Network3Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 3", "Perimeter Defense Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void Network4Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 4", "Emergency Response Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void Network5Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 5", "Advanced Reconnaissance Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void Network6Button_Click(object sender, RoutedEventArgs e)
        {
            var networkDetailsWindow = new NetworkDetailsPage("Network 6", "Maritime Surveillance Network");
            networkDetailsWindow.Owner = this;
            networkDetailsWindow.Show();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
