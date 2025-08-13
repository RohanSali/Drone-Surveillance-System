using System;
using System.Collections.Generic;
using System.Linq;

namespace DroneSurveillanceSystem.Services
{
    public static class DeviceDataManager
    {
        private static readonly List<UsbDrone> _persistentDrones = new List<UsbDrone>();
        private static readonly List<UsbCctv> _persistentCctvs = new List<UsbCctv>();

        // Events to notify when data changes
        public static event Action<List<UsbDrone>> DronesChanged;
        public static event Action<List<UsbCctv>> CctvsChanged;

        // Get all drones (both persistent and detected)
        public static List<UsbDrone> GetAllDrones()
        {
            return _persistentDrones.ToList();
        }

        // Get all CCTVs (both persistent and detected)
        public static List<UsbCctv> GetAllCctvs()
        {
            return _persistentCctvs.ToList();
        }

        // Add a new drone
        public static void AddDrone(UsbDrone drone)
        {
            if (drone == null) return;

            // Check for duplicates by DeviceId
            if (_persistentDrones.Any(d => d.DeviceId == drone.DeviceId))
            {
                throw new InvalidOperationException($"A drone with Device ID '{drone.DeviceId}' already exists.");
            }

            _persistentDrones.Add(drone);
            DronesChanged?.Invoke(_persistentDrones.ToList());
        }

        // Add a new CCTV
        public static void AddCctv(UsbCctv cctv)
        {
            if (cctv == null) return;

            // Check for duplicates by DeviceId
            if (_persistentCctvs.Any(c => c.DeviceId == cctv.DeviceId))
            {
                throw new InvalidOperationException($"A CCTV with Device ID '{cctv.DeviceId}' already exists.");
            }

            _persistentCctvs.Add(cctv);
            CctvsChanged?.Invoke(_persistentCctvs.ToList());
        }

        // Remove a drone
        public static bool RemoveDrone(UsbDrone drone)
        {
            if (drone == null) return false;

            var removed = _persistentDrones.Remove(drone);
            if (removed)
            {
                DronesChanged?.Invoke(_persistentDrones.ToList());
            }
            return removed;
        }

        // Remove a CCTV
        public static bool RemoveCctv(UsbCctv cctv)
        {
            if (cctv == null) return false;

            var removed = _persistentCctvs.Remove(cctv);
            if (removed)
            {
                CctvsChanged?.Invoke(_persistentCctvs.ToList());
            }
            return removed;
        }

        // Remove drone by DeviceId
        public static bool RemoveDroneById(string deviceId)
        {
            var drone = _persistentDrones.FirstOrDefault(d => d.DeviceId == deviceId);
            if (drone != null)
            {
                return RemoveDrone(drone);
            }
            return false;
        }

        // Remove CCTV by DeviceId
        public static bool RemoveCctvById(string deviceId)
        {
            var cctv = _persistentCctvs.FirstOrDefault(c => c.DeviceId == deviceId);
            if (cctv != null)
            {
                return RemoveCctv(cctv);
            }
            return false;
        }

        // Clear all data (for testing purposes)
        public static void ClearAllData()
        {
            _persistentDrones.Clear();
            _persistentCctvs.Clear();
            DronesChanged?.Invoke(new List<UsbDrone>());
            CctvsChanged?.Invoke(new List<UsbCctv>());
        }

        // Get count of drones
        public static int DroneCount => _persistentDrones.Count;

        // Get count of CCTVs
        public static int CctvCount => _persistentCctvs.Count;
    }
}
