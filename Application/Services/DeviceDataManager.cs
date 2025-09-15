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
        private static readonly Dictionary<string, DateTime> _droneLastSeenUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static System.Threading.Timer? _presenceTimer;

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
        
        public static string GetCurrentUser()
        {
            lock (_lockObject)
            {
                return _currentUserKey;
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

                // Ensure defaults are inactive on load
                foreach (var d in _persistentDrones)
                {
                    d.IsConnected = false;
                    if (string.IsNullOrWhiteSpace(d.Status) || d.Status.Equals("Connected - Ready for Operations", StringComparison.OrdinalIgnoreCase))
                        d.Status = "Disconnected";
                }
                foreach (var c in _persistentCctvs)
                {
                    c.IsConnected = false;
                    if (string.IsNullOrWhiteSpace(c.Status) || c.Status.Equals("Ready for Connection", StringComparison.OrdinalIgnoreCase))
                        c.Status = "Disconnected";
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
                EnsurePresenceTimerStarted();
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

        // Presence handling for incoming telemetry
        private static void EnsurePresenceTimerStarted()
        {
            if (_presenceTimer != null) return;
            _presenceTimer = new System.Threading.Timer(CheckPresence, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        private static void CheckPresence(object? state)
        {
            try
            {
                lock (_lockObject)
                {
                    var nowUtc = DateTime.UtcNow;
                    bool changed = false;
                    foreach (var drone in _persistentDrones)
                    {
                        var key = string.IsNullOrWhiteSpace(drone.DeviceId) ? drone.Name : drone.DeviceId;
                        if (string.IsNullOrWhiteSpace(key)) continue;
                        if (_droneLastSeenUtc.TryGetValue(key, out var lastSeen))
                        {
                            if (nowUtc - lastSeen > TimeSpan.FromMinutes(10))
                            {
                                if (drone.IsConnected || !string.Equals(drone.Status, "Disconnected", StringComparison.OrdinalIgnoreCase))
                                {
                                    drone.IsConnected = false;
                                    drone.Status = "Disconnected";
                                    changed = true;
                                }
                            }
                        }
                        else
                        {
                            if (drone.IsConnected)
                            {
                                drone.IsConnected = false;
                                drone.Status = "Disconnected";
                                changed = true;
                            }
                        }
                    }
                    if (changed)
                    {
                        DronesChanged?.Invoke(_persistentDrones.ToList());
                        SaveForCurrentUser();
                    }
                }
            }
            catch { }
        }

        public static void UpdateDroneFromPositionMessage(string droneId, double[]? positionArray, string? batteryStatus, string? status, DateTime? timestampUtc)
        {
            if (string.IsNullOrWhiteSpace(droneId)) return;
            lock (_lockObject)
            {
                // Match by DeviceId first, then by Name
                var drone = _persistentDrones.FirstOrDefault(d => string.Equals(d.DeviceId, droneId, StringComparison.OrdinalIgnoreCase))
                            ?? _persistentDrones.FirstOrDefault(d => string.Equals(d.Name, droneId, StringComparison.OrdinalIgnoreCase));

                if (drone == null)
                {
                    // If unknown drone arrives, create a lightweight entry so UI reflects it
                    drone = new UsbDrone
                    {
                        Name = droneId,
                        DeviceId = droneId,
                        UsbPort = "",
                        DroneType = "Surveillance",
                        FirmwareVersion = "",
                    };
                    _persistentDrones.Add(drone);
                }

                bool changed = false;

                // Mark as connected/active on any telemetry
                if (!drone.IsConnected) { drone.IsConnected = true; changed = true; }

                // Update status only if provided
                if (!string.IsNullOrWhiteSpace(status) && !string.Equals(drone.Status, status, StringComparison.Ordinal))
                {
                    drone.Status = status;
                    changed = true;
                }

                // Try to parse battery if numeric (ignore unknown)
                if (!string.IsNullOrWhiteSpace(batteryStatus) && int.TryParse(batteryStatus, out var batteryPct))
                {
                    var clamped = Math.Max(0, Math.Min(100, batteryPct));
                    if (drone.BatteryLevel != clamped)
                    {
                        drone.BatteryLevel = clamped;
                        changed = true;
                    }
                }

                // Update last seen
                var seen = timestampUtc?.ToUniversalTime() ?? DateTime.UtcNow;
                var key = string.IsNullOrWhiteSpace(drone.DeviceId) ? drone.Name : drone.DeviceId;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _droneLastSeenUtc[key] = seen;
                }

                if (changed)
                {
                    // Persist and notify only if something changed
                    DronesChanged?.Invoke(_persistentDrones.ToList());
                    SaveForCurrentUser();
                }

                // Ensure presence timer is running
                EnsurePresenceTimerStarted();
            }
        }
    }

    internal class UserDevicesPayload
    {
        public List<UsbDrone>? Drones { get; set; }
        public List<UsbCctv>? Cctvs { get; set; }
    }
}
