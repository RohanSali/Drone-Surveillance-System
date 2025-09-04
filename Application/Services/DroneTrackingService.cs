using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using DroneSurveillanceSystem.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DroneSurveillanceSystem.Services
{
    public class DroneTrackingService : INotifyPropertyChanged
    {
        private static readonly Lazy<DroneTrackingService> _instance = new Lazy<DroneTrackingService>(() => new DroneTrackingService());
        public static DroneTrackingService Instance => _instance.Value;
        private readonly Timer _trackingTimer;
        private readonly List<DroneTrackingData> _trackingHistory;
        private readonly Dictionary<string, DronePosition> _activeDrones;
        private readonly Random _random = new Random();

        // Real-time tracking properties
        private int _totalDronesTracked = 0;
        private int _activeDronesCount = 0;
        private string _trackingStatus = "Initializing";
        private double _averageSpeed = 0.0;
        private double _totalDistance = 0.0;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DroneTrackingEventArgs>? DronePositionUpdated;
        public event EventHandler<string>? TrackingAlert;

        // Properties for UI binding
        public int TotalDronesTracked
        {
            get => _totalDronesTracked;
            private set { _totalDronesTracked = value; OnPropertyChanged(); }
        }

        public int ActiveDronesCount
        {
            get => _activeDronesCount;
            private set { _activeDronesCount = value; OnPropertyChanged(); }
        }

        public string TrackingStatus
        {
            get => _trackingStatus;
            private set { _trackingStatus = value; OnPropertyChanged(); }
        }

        public double AverageSpeed
        {
            get => _averageSpeed;
            private set { _averageSpeed = value; OnPropertyChanged(); }
        }

        public double TotalDistance
        {
            get => _totalDistance;
            private set { _totalDistance = value; OnPropertyChanged(); }
        }

        public List<DronePosition> ActiveDronePositions => _activeDrones.Values
            .Where(d => d.LastSeen != default && DateTime.UtcNow - d.LastSeen.ToUniversalTime() <= TimeSpan.FromMinutes(10))
            .Where(d => !double.IsNaN(d.Latitude) && !double.IsNaN(d.Longitude))
            .ToList();

        public DroneTrackingService()
        {
            _trackingHistory = new List<DroneTrackingData>();
            _activeDrones = new Dictionary<string, DronePosition>();
            
            // Start tracking timer - update every 2 seconds
            _trackingTimer = new Timer(UpdateTracking, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            
            TrackingStatus = "Active";
        }

        private void UpdateTracking(object? state)
        {
            try
            {
                var speeds = new List<double>();
                var totalMovement = 0.0;

                foreach (var drone in _activeDrones.Values)
                {
                    // Calculate movement distance and speed based on last history point if any
                    var last = _trackingHistory.LastOrDefault(h => h.DroneId == drone.Id);
                    double distance = 0.0;
                    if (last != null)
                    {
                        distance = CalculateDistance(last.Position.Latitude, last.Position.Longitude, drone.Latitude, drone.Longitude);
                    }
                    totalMovement += distance;
                    var speed = distance * 30; // per minute approximation
                    speeds.Add(speed);
                    drone.Speed = speed;
                    
                    // Create tracking data
                    var trackingData = new DroneTrackingData
                    {
                        DroneId = drone.Id,
                        Timestamp = DateTime.Now,
                        Position = new DronePosition 
                        { 
                            Latitude = drone.Latitude, 
                            Longitude = drone.Longitude, 
                            Altitude = drone.Altitude,
                            Status = drone.Status,
                            Speed = drone.Speed
                        },
                        BatteryLevel = drone.BatteryLevel,
                        SignalStrength = drone.SignalStrength
                    };
                    
                    _trackingHistory.Add(trackingData);
                    
                    // Fire position update event
                    DronePositionUpdated?.Invoke(this, new DroneTrackingEventArgs 
                    { 
                        DroneId = drone.Id, 
                        Position = drone,
                        TrackingData = trackingData
                    });
                }

                // Update aggregate statistics
                TotalDistance += totalMovement;
                AverageSpeed = speeds.Any() ? speeds.Average() : 0.0;
                
                // Clean up old tracking history (keep last 1000 entries per drone)
                CleanupTrackingHistory();
                
                // Check for alerts
                CheckForTrackingAlerts();
            }
            catch (Exception ex)
            {
                TrackingAlert?.Invoke(this, $"Tracking update error: {ex.Message}");
            }
        }

        // Simulation removed: telemetry updates provided via UpdateFromTelemetry

        public void UpdateFromTelemetry(string droneId, double latitude, double longitude, double altitude, double? batteryPercent, string? status, DateTime? seenUtc = null)
        {
            if (string.IsNullOrWhiteSpace(droneId)) return;
            var id = droneId.Trim();
            if (!_activeDrones.TryGetValue(id, out var drone))
            {
                drone = new DronePosition { Id = id, Name = id };
                _activeDrones[id] = drone;
                TotalDronesTracked = _activeDrones.Count;
            }
            drone.Latitude = latitude;
            drone.Longitude = longitude;
            drone.Altitude = altitude;
            if (batteryPercent.HasValue)
            {
                drone.BatteryLevel = Math.Max(0, Math.Min(100, batteryPercent.Value));
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                switch (status.Trim().ToLowerInvariant())
                {
                    case "active":
                    case "flying":
                        drone.Status = DroneFlightStatus.Flying; break;
                    case "hovering":
                        drone.Status = DroneFlightStatus.Hovering; break;
                    case "landing":
                        drone.Status = DroneFlightStatus.Landing; break;
                    case "returning":
                        drone.Status = DroneFlightStatus.Returning; break;
                    case "takingoff":
                    case "taking_off":
                        drone.Status = DroneFlightStatus.TakingOff; break;
                    case "emergency":
                        drone.Status = DroneFlightStatus.Emergency; break;
                    default:
                        drone.Status = DroneFlightStatus.Grounded; break;
                }
            }
            drone.LastSeen = (seenUtc ?? DateTime.UtcNow).ToLocalTime();
            ActiveDronesCount = _activeDrones.Values.Count(d => DateTime.UtcNow - d.LastSeen.ToUniversalTime() <= TimeSpan.FromMinutes(10));
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadius = 6371000; // meters
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadius * c;
        }

        private void CleanupTrackingHistory()
        {
            if (_trackingHistory.Count > 3000) // Keep last 3000 entries total
            {
                var toRemove = _trackingHistory.Count - 3000;
                _trackingHistory.RemoveRange(0, toRemove);
            }
        }

        private void CheckForTrackingAlerts()
        {
            foreach (var drone in _activeDrones.Values)
            {
                // Low battery alert
                if (drone.BatteryLevel < 20)
                {
                    TrackingAlert?.Invoke(this, $"LOW BATTERY: {drone.Name} - {drone.BatteryLevel:F1}%");
                }
                
                // Lost signal alert
                if (drone.SignalStrength < 30)
                {
                    TrackingAlert?.Invoke(this, $"WEAK SIGNAL: {drone.Name} - {drone.SignalStrength:F0}%");
                }
                
                // Out of bounds alert (example bounds)
                if (Math.Abs(drone.Latitude - 37.7749) > 0.01 || Math.Abs(drone.Longitude + 122.4194) > 0.01)
                {
                    TrackingAlert?.Invoke(this, $"OUT OF BOUNDS: {drone.Name}");
                }
            }
        }

        public async Task<List<DroneTrackingData>> GetTrackingHistoryAsync(string droneId, DateTime? fromTime = null, DateTime? toTime = null)
        {
            return await Task.Run(() =>
            {
                var query = _trackingHistory.Where(h => h.DroneId == droneId);
                
                if (fromTime.HasValue)
                    query = query.Where(h => h.Timestamp >= fromTime.Value);
                    
                if (toTime.HasValue)
                    query = query.Where(h => h.Timestamp <= toTime.Value);
                
                return query.OrderBy(h => h.Timestamp).ToList();
            });
        }

        public async Task<bool> AddDroneToTrackingAsync(DronePosition drone)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_activeDrones.ContainsKey(drone.Id))
                    {
                        _activeDrones[drone.Id] = drone;
                        ActiveDronesCount = _activeDrones.Count;
                        TotalDronesTracked++;
                        
                        TrackingAlert?.Invoke(this, $"Drone {drone.Name} added to tracking");
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        public async Task<bool> RemoveDroneFromTrackingAsync(string droneId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (_activeDrones.Remove(droneId))
                    {
                        ActiveDronesCount = _activeDrones.Count;
                        TrackingAlert?.Invoke(this, $"Drone {droneId} removed from tracking");
                        return true;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }

        public DronePosition? GetDronePosition(string droneId)
        {
            return _activeDrones.TryGetValue(droneId, out var position) ? position : null;
        }

        public void StartTracking()
        {
            TrackingStatus = "Active";
            TrackingAlert?.Invoke(this, "Drone tracking started");
        }

        public void StopTracking()
        {
            TrackingStatus = "Stopped";
            TrackingAlert?.Invoke(this, "Drone tracking stopped");
        }

        public void Dispose()
        {
            _trackingTimer?.Dispose();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Supporting classes
    public class DroneTrackingData
    {
        public string DroneId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public DronePosition Position { get; set; } = new();
        public double BatteryLevel { get; set; }
        public double SignalStrength { get; set; }
    }

    public class DronePosition
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double BatteryLevel { get; set; } = 100;
        public double SignalStrength { get; set; } = 85;
        public DroneFlightStatus Status { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public int CasualtiesDetected { get; set; } = 0;
        public int AnomaliesDetected { get; set; } = 0;
        public DateTime LastCasualtyTime { get; set; }
        public DateTime LastAnomalyTime { get; set; }
        
        public string StatusText => Status.ToString();
        public string CoordinatesText => $"{Latitude:F6}, {Longitude:F6}";
        public string AltitudeText => $"{Altitude:F1}m";
        public string SpeedText => $"{Speed:F1} m/min";
        public string BatteryText => $"{BatteryLevel:F1}%";
        public string SignalText => $"{SignalStrength:F0}%";
        public string CasualtiesText => $"{CasualtiesDetected} detected";
        public string AnomaliesText => $"{AnomaliesDetected} detected";
    }

    public enum DroneFlightStatus
    {
        Grounded,
        TakingOff,
        Flying,
        Hovering,
        Landing,
        Returning,
        Emergency
    }

    public class DroneTrackingEventArgs : EventArgs
    {
        public string DroneId { get; set; } = string.Empty;
        public DronePosition Position { get; set; } = new();
        public DroneTrackingData TrackingData { get; set; } = new();
    }
}
