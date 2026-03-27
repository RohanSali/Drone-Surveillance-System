using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using DroneSurveillanceSystem.Services.Firebase;
using System.Text.Json.Serialization;

namespace DroneSurveillanceSystem.Services
{
    public static class DeviceDataManager
    {
        private static readonly List<UsbDrone> _persistentDrones = new List<UsbDrone>();
        private static readonly List<UsbCctv> _persistentCctvs = new List<UsbCctv>();
        private static string _currentUserKey = "guest";
        private static readonly object _lockObject = new object();
        private static readonly Dictionary<string, DateTime> _droneLastSeenUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _droneAccessAllowed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, bool> _cctvAccessAllowed = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
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
                var realtimePayload = LoadRealtimeCache();
                var droneRealtime = (realtimePayload?.Drones ?? new List<DroneRealtimeState>())
                    .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
                    .ToDictionary(d => d.DeviceId, d => d, StringComparer.OrdinalIgnoreCase);
                var cctvRealtime = (realtimePayload?.Cctvs ?? new List<CctvRealtimeState>())
                    .Where(c => !string.IsNullOrWhiteSpace(c.DeviceId))
                    .ToDictionary(c => c.DeviceId, c => c, StringComparer.OrdinalIgnoreCase);

                _persistentDrones.Clear();
                _persistentCctvs.Clear();
                _droneLastSeenUtc.Clear();
                _droneAccessAllowed.Clear();
                _cctvAccessAllowed.Clear();

                // Static metadata is loaded from RTDB only.
                var (rtdbDrones, rtdbCctvs) = LoadMetadataFromFirebase();
                _persistentDrones.AddRange(rtdbDrones);
                _persistentCctvs.AddRange(rtdbCctvs);

                // Overlay local realtime cache over RTDB metadata.
                foreach (var d in _persistentDrones)
                {
                    d.IsConnected = false;
                    d.Status = "Disconnected";
                    var allowed = _droneAccessAllowed.TryGetValue(d.DeviceId, out var a) && a;
                    if (droneRealtime.TryGetValue(d.DeviceId, out var rt))
                    {
                        if (allowed)
                        {
                            d.Status = string.IsNullOrWhiteSpace(rt.Status) ? "Disconnected" : rt.Status;
                            d.IsConnected = rt.IsConnected;
                        }
                        else
                        {
                            d.Status = "Disconnected";
                            d.IsConnected = false;
                        }
                        d.BatteryLevel = rt.BatteryLevel;
                        if (rt.LastSeenUtc.HasValue)
                        {
                            if (allowed) _droneLastSeenUtc[d.DeviceId] = rt.LastSeenUtc.Value;
                        }
                    }
                }

                foreach (var c in _persistentCctvs)
                {
                    c.IsConnected = false;
                    c.Status = "Disconnected";
                    var allowed = _cctvAccessAllowed.TryGetValue(c.DeviceId, out var a) && a;
                    if (cctvRealtime.TryGetValue(c.DeviceId, out var rt))
                    {
                        if (allowed)
                        {
                            c.Status = string.IsNullOrWhiteSpace(rt.Status) ? "Disconnected" : rt.Status;
                            c.IsConnected = rt.IsConnected;
                        }
                        else
                        {
                            c.Status = "Disconnected";
                            c.IsConnected = false;
                        }
                    }
                }

                // Migrate any legacy local payload to realtime-only cache format.
                SaveForCurrentUser();
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

        private static UserDevicesPayload? LoadRealtimeCache()
        {
            try
            {
                var path = GetUserDevicesPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<UserDevicesPayload>(json);
                }
            }
            catch
            {
                // ignore and fallback to empty realtime cache
            }
            return null;
        }

        private static (List<UsbDrone> drones, List<UsbCctv> cctvs) LoadMetadataFromFirebase()
        {
            try
            {
                var session = FirebaseSession.Current;
                if (session == null || string.IsNullOrWhiteSpace(session.FirebaseIdToken))
                    return (new List<UsbDrone>(), new List<UsbCctv>());

                var config = FirebaseAuthConfig.Load();
                using var http = new System.Net.Http.HttpClient();
                var rtdb = new FirebaseRtdbRestClient(http, config);
                var mappingService = new FirebaseUserClientMappingService(rtdb);
                var appId = session.AppClientId;
                if (string.IsNullOrWhiteSpace(appId))
                    return (new List<UsbDrone>(), new List<UsbCctv>());

                var droneMappings = mappingService.GetDroneMappingsAsync(
                    appId,
                    session.FirebaseIdToken,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                var cctvMappings = mappingService.GetCctvMappingsAsync(
                    appId,
                    session.FirebaseIdToken,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult();

                var mappedDroneIds = new HashSet<string>(
                    droneMappings.Values
                        .Select(m => !string.IsNullOrWhiteSpace(m.Id) ? m.Id.Trim() : string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x)),
                    StringComparer.OrdinalIgnoreCase);

                var mappedCctvIds = new HashSet<string>(
                    cctvMappings.Values
                        .Select(m => !string.IsNullOrWhiteSpace(m.Id) ? m.Id.Trim() : string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x)),
                    StringComparer.OrdinalIgnoreCase);

                var droneMap = rtdb.GetAsync<Dictionary<string, DroneMetadataRecord>>(
                    "drones",
                    session.FirebaseIdToken,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult()
                    ?? new Dictionary<string, DroneMetadataRecord>(StringComparer.OrdinalIgnoreCase);

                var cctvMap = rtdb.GetAsync<Dictionary<string, CctvMetadataRecord>>(
                    "cctvs",
                    session.FirebaseIdToken,
                    System.Threading.CancellationToken.None).GetAwaiter().GetResult()
                    ?? new Dictionary<string, CctvMetadataRecord>(StringComparer.OrdinalIgnoreCase);

                var drones = new List<UsbDrone>();
                foreach (var kvp in droneMap)
                {
                    if (kvp.Value == null) continue;
                    var id = kvp.Value.DeviceId?.Trim();
                    if (string.IsNullOrWhiteSpace(id))
                        id = kvp.Key?.Trim();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (!mappedDroneIds.Contains(id)) continue;

                    var allowed = !string.IsNullOrWhiteSpace(kvp.Value.DeviceAccessing)
                                   && string.Equals(kvp.Value.DeviceAccessing.Trim(), appId.Trim(), StringComparison.OrdinalIgnoreCase);
                    _droneAccessAllowed[id] = allowed;

                    drones.Add(new UsbDrone
                    {
                        DeviceId = id,
                        Name = kvp.Value.Name?.Trim() ?? id,
                        DroneType = kvp.Value.Type?.Trim() ?? "Surveillance",
                        FirmwareVersion = kvp.Value.FirmwareVersion?.Trim() ?? "",
                        UsbPort = kvp.Value.UsbPort?.Trim() ?? "",
                        BluetoothMacAddress = kvp.Value.BluetoothMacAddress?.Trim() ?? "",
                        IpAddress = kvp.Value.IpAddress?.Trim() ?? "",
                        SimType = kvp.Value.SimType?.Trim() ?? ""
                    });
                }

                var cctvs = new List<UsbCctv>();
                foreach (var kvp in cctvMap)
                {
                    if (kvp.Value == null) continue;
                    var id = kvp.Value.DeviceId?.Trim();
                    if (string.IsNullOrWhiteSpace(id))
                        id = kvp.Key?.Trim();
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (!mappedCctvIds.Contains(id)) continue;

                    var allowed = !string.IsNullOrWhiteSpace(kvp.Value.DeviceAccessing)
                                   && string.Equals(kvp.Value.DeviceAccessing.Trim(), appId.Trim(), StringComparison.OrdinalIgnoreCase);
                    _cctvAccessAllowed[id] = allowed;

                    cctvs.Add(new UsbCctv
                    {
                        DeviceId = id,
                        Name = kvp.Value.Name?.Trim() ?? id,
                        FirmwareVersion = kvp.Value.FirmwareVersion?.Trim() ?? "",
                        UsbPort = kvp.Value.UsbPort?.Trim() ?? "",
                        BluetoothMacAddress = kvp.Value.BluetoothMacAddress?.Trim() ?? "",
                        IpAddress = kvp.Value.IpAddress?.Trim() ?? "",
                        SimType = kvp.Value.SimType?.Trim() ?? "",
                        Resolution = kvp.Value.Resolution?.Trim() ?? "1080p",
                        FrameRate = kvp.Value.FrameRate > 0 ? kvp.Value.FrameRate : 30
                    });
                }

                foreach (var mapping in droneMappings.Values)
                {
                    if (string.IsNullOrWhiteSpace(mapping.Id)) continue;
                    if (drones.Any(d => d.DeviceId.Equals(mapping.Id, StringComparison.OrdinalIgnoreCase))) continue;
                    drones.Add(new UsbDrone
                    {
                        DeviceId = mapping.Id,
                        Name = string.IsNullOrWhiteSpace(mapping.Name) ? mapping.Id : mapping.Name,
                        DroneType = "Surveillance",
                        Status = "Disconnected",
                        IsConnected = false
                    });
                    _droneAccessAllowed[mapping.Id] = false;
                }

                foreach (var mapping in cctvMappings.Values)
                {
                    if (string.IsNullOrWhiteSpace(mapping.Id)) continue;
                    if (cctvs.Any(c => c.DeviceId.Equals(mapping.Id, StringComparison.OrdinalIgnoreCase))) continue;
                    cctvs.Add(new UsbCctv
                    {
                        DeviceId = mapping.Id,
                        Name = string.IsNullOrWhiteSpace(mapping.Name) ? mapping.Id : mapping.Name,
                        Resolution = "1080p",
                        FrameRate = 30,
                        Status = "Disconnected",
                        IsConnected = false
                    });
                    _cctvAccessAllowed[mapping.Id] = false;
                }

                return (drones, cctvs);
            }
            catch
            {
                return (new List<UsbDrone>(), new List<UsbCctv>());
            }
        }

        private static void SaveForCurrentUser()
        {
            try
            {
                var path = GetUserDevicesPath();
                var payload = new UserDevicesPayload
                {
                    Drones = _persistentDrones
                        .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
                        .Select(d => new DroneRealtimeState
                        {
                            DeviceId = d.DeviceId,
                            Status = d.Status,
                            IsConnected = d.IsConnected,
                            BatteryLevel = d.BatteryLevel,
                            LastSeenUtc = _droneLastSeenUtc.TryGetValue(d.DeviceId, out var ts) ? ts : null
                        })
                        .ToList(),
                    Cctvs = _persistentCctvs
                        .Where(c => !string.IsNullOrWhiteSpace(c.DeviceId))
                        .Select(c => new CctvRealtimeState
                        {
                            DeviceId = c.DeviceId,
                            Status = c.Status,
                            IsConnected = c.IsConnected
                        })
                        .ToList()
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        // Events to notify when data changes
        public static event Action<List<UsbDrone>> DronesChanged;
        public static event Action<List<UsbCctv>> CctvsChanged;

        public static bool IsDroneAccessAllowed(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return false;
            lock (_lockObject)
            {
                return _droneAccessAllowed.TryGetValue(deviceId.Trim(), out var allowed) && allowed;
            }
        }

        public static bool IsCctvAccessAllowed(string? deviceId)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return false;
            lock (_lockObject)
            {
                return _cctvAccessAllowed.TryGetValue(deviceId.Trim(), out var allowed) && allowed;
            }
        }

        public static void SetDroneAccessAllowed(string? deviceId, bool allowed)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;
            lock (_lockObject)
            {
                var key = deviceId.Trim();
                _droneAccessAllowed[key] = allowed;

                var drone = _persistentDrones.FirstOrDefault(d => string.Equals(d.DeviceId, key, StringComparison.OrdinalIgnoreCase));
                if (drone != null)
                {
                    drone.IsConnected = false;
                    drone.Status = "Disconnected";
                }

                DronesChanged?.Invoke(_persistentDrones.ToList());
                SaveForCurrentUser();
            }
        }

        public static void SetCctvAccessAllowed(string? deviceId, bool allowed)
        {
            if (string.IsNullOrWhiteSpace(deviceId)) return;
            lock (_lockObject)
            {
                var key = deviceId.Trim();
                _cctvAccessAllowed[key] = allowed;

                var cctv = _persistentCctvs.FirstOrDefault(c => string.Equals(c.DeviceId, key, StringComparison.OrdinalIgnoreCase));
                if (cctv != null)
                {
                    cctv.IsConnected = false;
                    cctv.Status = "Disconnected";
                }

                CctvsChanged?.Invoke(_persistentCctvs.ToList());
                SaveForCurrentUser();
            }
        }

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

            var existing = _persistentDrones.FirstOrDefault(d =>
                !string.IsNullOrWhiteSpace(d.DeviceId) &&
                d.DeviceId.Equals(drone.DeviceId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Update in-memory representation (metadata source remains RTDB).
                existing.Name = drone.Name;
                existing.DeviceId = drone.DeviceId;
                existing.UsbPort = drone.UsbPort;
                existing.DroneType = drone.DroneType;
                existing.FirmwareVersion = drone.FirmwareVersion;
                existing.BluetoothMacAddress = drone.BluetoothMacAddress;
                existing.IpAddress = drone.IpAddress;
                existing.SimType = drone.SimType;
                existing.Location = drone.Location;
            }
            else
            {
                _persistentDrones.Add(drone);
            }
            DronesChanged?.Invoke(_persistentDrones.ToList());
            SaveForCurrentUser();
        }

        // Add a new CCTV
        public static void AddCctv(UsbCctv cctv)
        {
            if (cctv == null) return;

            var existing = _persistentCctvs.FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c.DeviceId) &&
                c.DeviceId.Equals(cctv.DeviceId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                // Update in-memory representation (metadata source remains RTDB).
                existing.Name = cctv.Name;
                existing.DeviceId = cctv.DeviceId;
                existing.UsbPort = cctv.UsbPort;
                existing.FirmwareVersion = cctv.FirmwareVersion;
                existing.Resolution = cctv.Resolution;
                existing.FrameRate = cctv.FrameRate;
                existing.BluetoothMacAddress = cctv.BluetoothMacAddress;
                existing.IpAddress = cctv.IpAddress;
                existing.SimType = cctv.SimType;
                existing.Location = cctv.Location;
            }
            else
            {
                _persistentCctvs.Add(cctv);
            }
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
                if (!string.IsNullOrWhiteSpace(drone.DeviceId))
                    _droneAccessAllowed.Remove(drone.DeviceId.Trim());
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
                if (!string.IsNullOrWhiteSpace(cctv.DeviceId))
                    _cctvAccessAllowed.Remove(cctv.DeviceId.Trim());
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
            _droneAccessAllowed.Clear();
            _cctvAccessAllowed.Clear();
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
                    // Ignore telemetry for drones that are not mapped to current user.
                    return;
                }

                // Ignore telemetry unless this device is locked to the current appId.
                var allowedTelemetry = _droneAccessAllowed.TryGetValue(drone.DeviceId, out var allowed) && allowed;
                if (!allowedTelemetry) return;

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
        public List<DroneRealtimeState>? Drones { get; set; }
        public List<CctvRealtimeState>? Cctvs { get; set; }
    }

    internal class DroneRealtimeState
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = "Disconnected";
        public bool IsConnected { get; set; }
        public int BatteryLevel { get; set; }
        public DateTime? LastSeenUtc { get; set; }
    }

    internal class CctvRealtimeState
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Status { get; set; } = "Disconnected";
        public bool IsConnected { get; set; }
    }

    internal class DroneMetadataRecord
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public string UsbPort { get; set; } = string.Empty;
        public string BluetoothMacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string SimType { get; set; } = string.Empty;

        [JsonPropertyName("device_accessing")]
        public string? DeviceAccessing { get; set; }
    }

    internal class CctvMetadataRecord
    {
        public string DeviceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FirmwareVersion { get; set; } = string.Empty;
        public string UsbPort { get; set; } = string.Empty;
        public string BluetoothMacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string SimType { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public int FrameRate { get; set; }

        [JsonPropertyName("device_accessing")]
        public string? DeviceAccessing { get; set; }
    }
}
