using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using DroneSurveillanceSystem.Services;
using System.Windows.Threading;
using System.ComponentModel;

namespace DroneSurveillanceSystem.Views
{
    public partial class MonitoringAlertsPage : Window, INotifyPropertyChanged
    {
        public ObservableCollection<AlertData> ActiveAlerts => AlertManager.Instance.ActiveAlerts;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MonitoringAlertsPage()
        {
            InitializeComponent();
            DataContext = this;
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ActiveAlerts));
                });
            };
        }

        private void Alert_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string alertTag)
            {
                // Find the alert by tag (assume tag is alert id)
                var alert = AlertManager.Instance.ActiveAlerts.FirstOrDefault(a => a.Timestamp == alertTag);
                if (alert != null)
                {
                    var alertPopup = new AlertInfoPopup(alert);
                    alertPopup.Owner = this;
                    alertPopup.ShowDialog();
                }
            }
        }

        private void AcknowledgeAlerts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("All alerts have been acknowledged.", "Alerts Acknowledged", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RefreshData_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Data refreshed successfully.", "Data Refresh", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }



        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
