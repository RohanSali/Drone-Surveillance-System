using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using DroneSurveillanceSystem.Services;
using System.Windows.Threading;
using System.ComponentModel;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Views
{
    public partial class MonitoringAlertsPage : Window, INotifyPropertyChanged
    {
        // Show only alerts from user-added devices (not hardcoded ones)
        public ObservableCollection<AlertData> ActiveAlerts => new ObservableCollection<AlertData>(AlertManager.Instance.GetAllDeviceAlerts());
        
        // Filter properties
        private DeviceFilterType _currentFilter = DeviceFilterType.All;
        public ObservableCollection<DeviceDisplayItem> FilteredDevices { get; } = new ObservableCollection<DeviceDisplayItem>();
        public ObservableCollection<AlertData> FilteredAlerts { get; } = new ObservableCollection<AlertData>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public MonitoringAlertsPage()
        {
            InitializeComponent();
            Console.WriteLine($"[MonitoringAlertsPage] 🏗️ MonitoringAlertsPage constructor called - Window created");
            
            // Set up data context
            DataContext = this;
            
            // Subscribe to alert updates and refresh the filtered view
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ActiveAlerts));
                    ApplyFilter(); // Refresh filtered data when alerts change
                });
            };
            
            Console.WriteLine($"[MonitoringAlertsPage] ✅ MonitoringAlertsPage initialization completed");
            
            // Initialize filter
            UpdateFilterButtonText();
            ApplyFilter();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Console.WriteLine($"[MonitoringAlertsPage] 📱 Window source initialized - Window is now active");
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Console.WriteLine($"[MonitoringAlertsPage] 🎯 Window activated - Page is now in focus");
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            Console.WriteLine($"[MonitoringAlertsPage] 🔄 Window deactivated - Page lost focus");
        }

        public static bool IsMonitoringAlertsPageOpen()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MonitoringAlertsPage)
                {
                    Console.WriteLine($"[MonitoringAlertsPage] ✅ MonitoringAlertsPage is currently open and available");
                    return true;
                }
            }
            Console.WriteLine($"[MonitoringAlertsPage] ❌ MonitoringAlertsPage is not currently open");
            return false;
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            // Cycle through filter types
            _currentFilter = _currentFilter switch
            {
                DeviceFilterType.All => DeviceFilterType.DronesOnly,
                DeviceFilterType.DronesOnly => DeviceFilterType.CctvOnly,
                DeviceFilterType.CctvOnly => DeviceFilterType.All,
                _ => DeviceFilterType.All
            };

            // Update button text and apply filter
            UpdateFilterButtonText();
            ApplyFilter();
        }

        private void UpdateFilterButtonText()
        {
            FilterButton.Content = _currentFilter switch
            {
                DeviceFilterType.All => "🔍 All Devices",
                DeviceFilterType.DronesOnly => "🚁 Drones Only",
                DeviceFilterType.CctvOnly => "📷 CCTV Only",
                _ => "🔍 All Devices"
            };
        }

        private void ApplyFilter()
        {
            // Update devices list
            FilteredDevices.Clear();
            
            // Update title
            DevicesTitle.Text = _currentFilter switch
            {
                DeviceFilterType.All => "🚁📷 All Devices",
                DeviceFilterType.DronesOnly => "🚁 Active Drones",
                DeviceFilterType.CctvOnly => "📷 CCTV Cameras",
                _ => "🚁📷 All Devices"
            };

            // Add devices based on filter
            if (_currentFilter == DeviceFilterType.All || _currentFilter == DeviceFilterType.DronesOnly)
            {
                foreach (var drone in DeviceDataManager.GetAllDrones())
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = drone.Name,
                        Details = $"Device ID: {drone.DeviceId}",
                        AdditionalInfo = $"USB Port: {drone.UsbPort} | Firmware: {drone.FirmwareVersion}",
                        Status = drone.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusColor = drone.IsConnected ? "#88C999" : "#FF6B6B",
                        DeviceId = drone.DeviceId,
                        DeviceType = "Drone"
                    });
                }
            }

            if (_currentFilter == DeviceFilterType.All || _currentFilter == DeviceFilterType.CctvOnly)
            {
                foreach (var cctv in DeviceDataManager.GetAllCctvs())
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = cctv.Name,
                        Details = $"Device ID: {cctv.DeviceId}",
                        AdditionalInfo = $"Resolution: {cctv.Resolution} | Frame Rate: {cctv.FrameRate}fps",
                        Status = cctv.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusColor = cctv.IsConnected ? "#88C999" : "#FF6B6B",
                        DeviceId = cctv.DeviceId,
                        DeviceType = "CCTV"
                    });
                }
            }

            // Update alerts list
            FilteredAlerts.Clear();
            var allAlerts = AlertManager.Instance.GetAllDeviceAlerts();
            
            foreach (var alert in allAlerts)
            {
                // Check if this alert should be shown based on current filter
                bool shouldShow = _currentFilter switch
                {
                    DeviceFilterType.All => true,
                    DeviceFilterType.DronesOnly => IsDroneDevice(alert.DroneId),
                    DeviceFilterType.CctvOnly => IsCctvDevice(alert.DroneId),
                    _ => true
                };

                if (shouldShow)
                {
                    FilteredAlerts.Add(alert);
                }
            }

            // Notify UI of changes
            OnPropertyChanged(nameof(FilteredDevices));
            OnPropertyChanged(nameof(FilteredAlerts));
        }

        private bool IsDroneDevice(string deviceId)
        {
            return DeviceDataManager.GetAllDrones().Any(d => d.DeviceId == deviceId);
        }

        private bool IsCctvDevice(string deviceId)
        {
            return DeviceDataManager.GetAllCctvs().Any(c => c.DeviceId == deviceId);
        }

        private void AcknowledgeAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all active alerts
                AlertManager.Instance.ActiveAlerts.Clear();
                
                // Show success message
                MessageBox.Show("All alerts have been successfully acknowledged and cleared.", 
                              "Alerts Acknowledged", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
                
                // Update the UI
                OnPropertyChanged(nameof(ActiveAlerts));
                ApplyFilter(); // Refresh filtered alerts
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error acknowledging alerts: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }
    }

    public enum DeviceFilterType
    {
        All,
        DronesOnly,
        CctvOnly
    }

    public class DeviceDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string AdditionalInfo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#88C999";
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
    }
}