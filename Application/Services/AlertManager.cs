using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using DroneSurveillanceSystem.Views;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Services
{
    public class AlertManager
    {
        private static AlertManager? _instance;
        public static AlertManager Instance => _instance ??= new AlertManager();

        public ObservableCollection<AlertData> ActiveAlerts { get; } = new ObservableCollection<AlertData>();

        private AlertManager() { }

        /// <summary>
        /// Gets alerts filtered by network - only shows alerts from devices in the specified network
        /// </summary>
        public IEnumerable<AlertData> GetAlertsForNetwork(Network network)
        {
            if (network == null) return Enumerable.Empty<AlertData>();

            // Get all device IDs from the network (both drones and CCTVs)
            var networkDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Add drone IDs
            if (network.Drones != null)
            {
                foreach (var drone in network.Drones)
                {
                    if (!string.IsNullOrEmpty(drone.Id))
                        networkDeviceIds.Add(drone.Id);
                }
            }
            
            // Add CCTV IDs
            if (network.Cctvs != null)
            {
                foreach (var cctv in network.Cctvs)
                {
                    if (!string.IsNullOrEmpty(cctv.Id))
                        networkDeviceIds.Add(cctv.Id);
                }
            }

            // Filter alerts to only show those from devices in this network
            return ActiveAlerts.Where(alert => 
                !string.IsNullOrEmpty(alert.DroneId) && 
                networkDeviceIds.Contains(alert.DroneId));
        }

        /// <summary>
        /// Gets alerts for all user-added devices (global view)
        /// </summary>
        public IEnumerable<AlertData> GetAllDeviceAlerts()
        {
            // Get all device IDs from DeviceDataManager
            var allDeviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Add all drone IDs
            foreach (var drone in DeviceDataManager.GetAllDrones())
            {
                if (!string.IsNullOrEmpty(drone.DeviceId))
                    allDeviceIds.Add(drone.DeviceId);
            }
            
            // Add all CCTV IDs
            foreach (var cctv in DeviceDataManager.GetAllCctvs())
            {
                if (!string.IsNullOrEmpty(cctv.DeviceId))
                    allDeviceIds.Add(cctv.DeviceId);
            }

            // Filter alerts to only show those from user-added devices
            return ActiveAlerts.Where(alert => 
                !string.IsNullOrEmpty(alert.DroneId) && 
                allDeviceIds.Contains(alert.DroneId));
        }

        /// <summary>
        /// Clears all active alerts from the collection
        /// </summary>
        public void ClearAllAlerts()
        {
            ActiveAlerts.Clear();
        }

        /// <summary>
        /// Gets the current count of active alerts
        /// </summary>
        public int AlertCount => ActiveAlerts.Count;

        /// <summary>
        /// Checks if there are any active alerts
        /// </summary>
        public bool HasAlerts => ActiveAlerts.Count > 0;

        /// <summary>
        /// Manually clears all alerts (can be called from UI or other services)
        /// </summary>
        public void ManualClearAlerts()
        {
            ClearAllAlerts();
        }

        /// <summary>
        /// Resets the singleton instance to ensure a completely fresh start
        /// This should be called on application startup to clear any persistent data
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }
    }
} 