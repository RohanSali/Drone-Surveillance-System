using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

namespace DroneSurveillanceSystem.Services
{
    public static class DeviceDataManager
    {
        private static readonly List<UsbDrone> _persistentDrones = new List<UsbDrone>();
        private static readonly List<UsbCctv> _persistentCctvs = new List<UsbCctv>();
        private static string _currentUserKey = "guest";
        private static readonly object _lockObject = new object();

        private static string GetStorageDirectory()
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance", "Devices");
            try
            {
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
            }
            catch { }
            return baseDir;
        }

        private static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "guest";
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = input.Select(c => invalidChars.Contains(c) || !(char.IsLetterOrDigit(c) || c == '@' || c == '.') ? '_' : c).ToArray();
            return new string(chars);
        }

        private static string GetUserDevicesPath()
        {
            var fileName = $"devices_{Sanitize(_currentUserKey)}.json";
            return Path.Combine(GetStorageDirectory(), fileName);
        }

        public static void SetCurrentUser(string? userEmailOrId)
        {
            lock (_lockObject)
            {
                _currentUserKey = string.IsNullOrWhiteSpace(userEmailOrId) ? "guest" : userEmailOrId.Trim();
                LoadForCurrentUser();
            }
        }

        private static void LoadForCurrentUser()
        {
            try
            {
                var path = GetUserDevicesPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var payload = JsonSerializer.Deserialize<UserDevicesPayload>(json);
                    _persistentDrones.Clear();
                    _persistentCctvs.Clear();
                    if (payload?.Drones != null) _persistentDrones.AddRange(payload.Drones);
                    if (payload?.Cctvs != null) _persistentCctvs.AddRange(payload.Cctvs);
                }
                else
                {
                    _persistentDrones.Clear();
                    _persistentCctvs.Clear();
                }
            }
            catch
            {
                _persistentDrones.Clear();
                _persistentCctvs.Clear();
            }
            finally
            {
                DronesChanged?.Invoke(_persistentDrones.ToList());
                CctvsChanged?.Invoke(_persistentCctvs.ToList());
            }
        }

        private static void SaveForCurrentUser()
        {
            try
            {
                var path = GetUserDevicesPath();
                var payload = new UserDevicesPayload
                {
                    Drones = _persistentDrones.ToList(),
                    Cctvs = _persistentCctvs.ToList()
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

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
            SaveForCurrentUser();
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
            SaveForCurrentUser();
        }

        // Remove a drone
        public static bool RemoveDrone(UsbDrone drone)
        {
            if (drone == null) return false;

            var removed = _persistentDrones.Remove(drone);
            if (removed)
            {
                DronesChanged?.Invoke(_persistentDrones.ToList());
                SaveForCurrentUser();
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
                SaveForCurrentUser();
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
            SaveForCurrentUser();
        }

        // Get count of drones
        public static int DroneCount => _persistentDrones.Count;

        // Get count of CCTVs
        public static int CctvCount => _persistentCctvs.Count;
    }

    internal class UserDevicesPayload
    {
        public List<UsbDrone>? Drones { get; set; }
        public List<UsbCctv>? Cctvs { get; set; }
    }
}
