using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Services
{
    public enum DroneFlightMode
    {
        Manual,
        Autonomous,
        PatrolRoute,
        ReturnToBase,
        Emergency,
        Hover
    }

    public enum DroneCommand
    {
        TakeOff,
        Land,
        MoveUp,
        MoveDown,
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        RotateLeft,
        RotateRight,
        StartPatrol,
        StopPatrol,
        ReturnHome,
        EmergencyStop,
        CalibrateGPS,
        StartRecording,
        StopRecording
    }

    public class DroneControlService
    {
        private readonly NetworkService _networkService;
        private readonly Timer _flightDataUpdater;
        private readonly Random _random = new Random();

        // Flight data
        private double _altitude = 0.0;
        private double _latitude = 37.7749;
        private double _longitude = -122.4194;
        private double _batteryLevel = 100.0;
        private double _speed = 0.0;
        private string _currentZone = "Base";
        private DroneFlightMode _flightMode = DroneFlightMode.Manual;
        private bool _isFlying = false;
        private bool _isRecording = false;

        // Patrol route
        private List<GPSCoordinate> _patrolRoute = new();
        private int _currentWaypoint = 0;

        public event EventHandler<DroneStatusEventArgs>? DroneStatusChanged;
        public event EventHandler<string>? CommandExecuted;

        // Properties
        public double Altitude => _altitude;
        public double Latitude => _latitude;
        public double Longitude => _longitude;
        public double BatteryLevel => _batteryLevel;
        public double Speed => _speed;
        public string CurrentZone => _currentZone;
        public DroneFlightMode FlightMode => _flightMode;
        public bool IsFlying => _isFlying;
        public bool IsRecording => _isRecording;
        public List<GPSCoordinate> PatrolRoute => _patrolRoute;

        public DroneControlService(NetworkService networkService)
        {
            _networkService = networkService;
            
            // Update flight data every 2 seconds
            _flightDataUpdater = new Timer(UpdateFlightData, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
            
            InitializeDefaultPatrolRoute();
        }

        private void InitializeDefaultPatrolRoute()
        {
            _patrolRoute = new List<GPSCoordinate>
            {
                new GPSCoordinate { Latitude = 37.7749, Longitude = -122.4194, Altitude = 50, Zone = "Waypoint-1" },
                new GPSCoordinate { Latitude = 37.7759, Longitude = -122.4184, Altitude = 55, Zone = "Waypoint-2" },
                new GPSCoordinate { Latitude = 37.7769, Longitude = -122.4174, Altitude = 60, Zone = "Waypoint-3" },
                new GPSCoordinate { Latitude = 37.7759, Longitude = -122.4164, Altitude = 55, Zone = "Waypoint-4" },
                new GPSCoordinate { Latitude = 37.7749, Longitude = -122.4174, Altitude = 50, Zone = "Waypoint-5" }
            };
        }

        private void UpdateFlightData(object? state)
        {
            if (_isFlying)
            {
                // Simulate battery drain
                _batteryLevel = Math.Max(0, _batteryLevel - _random.NextDouble() * 0.1);

                // Simulate autonomous flight movement
                if (_flightMode == DroneFlightMode.PatrolRoute)
                {
                    SimulatePatrolMovement();
                }
                else if (_flightMode == DroneFlightMode.Autonomous)
                {
                    SimulateAutonomousMovement();
                }

                // Update speed based on movement
                _speed = _flightMode switch
                {
                    DroneFlightMode.PatrolRoute => 15.0 + _random.NextDouble() * 5.0,
                    DroneFlightMode.Autonomous => 10.0 + _random.NextDouble() * 8.0,
                    DroneFlightMode.ReturnToBase => 20.0 + _random.NextDouble() * 5.0,
                    _ => _random.NextDouble() * 3.0
                };

                NotifyStatusChange();
            }
        }

        private void SimulatePatrolMovement()
        {
            if (_patrolRoute.Count == 0) return;

            var targetWaypoint = _patrolRoute[_currentWaypoint];
            var distance = CalculateDistance(_latitude, _longitude, targetWaypoint.Latitude, targetWaypoint.Longitude);

            if (distance < 0.001) // Reached waypoint
            {
                _currentWaypoint = (_currentWaypoint + 1) % _patrolRoute.Count;
                _currentZone = targetWaypoint.Zone;
                targetWaypoint = _patrolRoute[_currentWaypoint];
            }

            // Move towards target
            var deltaLat = (targetWaypoint.Latitude - _latitude) * 0.1;
            var deltaLon = (targetWaypoint.Longitude - _longitude) * 0.1;
            var deltaAlt = (targetWaypoint.Altitude - _altitude) * 0.1;

            _latitude += deltaLat;
            _longitude += deltaLon;
            _altitude += deltaAlt;
        }

        private void SimulateAutonomousMovement()
        {
            // Random autonomous movement within bounds
            _latitude += (_random.NextDouble() - 0.5) * 0.0001;
            _longitude += (_random.NextDouble() - 0.5) * 0.0001;
            _altitude += (_random.NextDouble() - 0.5) * 2.0;
            
            // Keep altitude within reasonable bounds
            _altitude = Math.Max(10, Math.Min(100, _altitude));
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            return Math.Sqrt(Math.Pow(lat2 - lat1, 2) + Math.Pow(lon2 - lon1, 2));
        }

        public async Task<bool> ExecuteCommandAsync(DroneCommand command, Dictionary<string, object>? parameters = null)
        {
            try
            {
                var commandData = new Dictionary<string, object>
                {
                    ["command"] = command.ToString(),
                    ["timestamp"] = DateTime.Now,
                    ["parameters"] = parameters ?? new Dictionary<string, object>()
                };

                // Send command to drone via network
                var success = await _networkService.SendCommandToDroneAsync(command.ToString(), commandData);
                
                if (success)
                {
                    await ProcessCommandLocally(command, parameters);
                    CommandExecuted?.Invoke(this, $"Command {command} executed successfully");
                }
                else
                {
                    CommandExecuted?.Invoke(this, $"Failed to execute command {command}");
                }

                return success;
            }
            catch (Exception ex)
            {
                CommandExecuted?.Invoke(this, $"Error executing command {command}: {ex.Message}");
                return false;
            }
        }

        private async Task ProcessCommandLocally(DroneCommand command, Dictionary<string, object>? parameters)
        {
            switch (command)
            {
                case DroneCommand.TakeOff:
                    _isFlying = true;
                    _altitude = 30.0; // Default takeoff altitude
                    _flightMode = DroneFlightMode.Hover;
                    await Task.Delay(3000); // Simulate takeoff time
                    break;

                case DroneCommand.Land:
                    _isFlying = false;
                    _altitude = 0.0;
                    _speed = 0.0;
                    _flightMode = DroneFlightMode.Manual;
                    await Task.Delay(5000); // Simulate landing time
                    break;

                case DroneCommand.MoveUp:
                    _altitude += parameters?.ContainsKey("distance") == true ? (double)parameters["distance"] : 5.0;
                    _altitude = Math.Min(_altitude, 120); // Max altitude limit
                    break;

                case DroneCommand.MoveDown:
                    _altitude -= parameters?.ContainsKey("distance") == true ? (double)parameters["distance"] : 5.0;
                    _altitude = Math.Max(_altitude, 5); // Min altitude limit
                    break;

                case DroneCommand.StartPatrol:
                    _flightMode = DroneFlightMode.PatrolRoute;
                    _currentWaypoint = 0;
                    break;

                case DroneCommand.StopPatrol:
                    _flightMode = DroneFlightMode.Hover;
                    break;

                case DroneCommand.ReturnHome:
                    _flightMode = DroneFlightMode.ReturnToBase;
                    // Start moving towards base coordinates
                    break;

                case DroneCommand.EmergencyStop:
                    _flightMode = DroneFlightMode.Emergency;
                    _speed = 0.0;
                    break;

                case DroneCommand.StartRecording:
                    _isRecording = true;
                    break;

                case DroneCommand.StopRecording:
                    _isRecording = false;
                    break;

                case DroneCommand.CalibrateGPS:
                    await Task.Delay(2000); // Simulate GPS calibration
                    break;
            }

            NotifyStatusChange();
        }

        public void SetPatrolRoute(List<GPSCoordinate> route)
        {
            _patrolRoute = new List<GPSCoordinate>(route);
            _currentWaypoint = 0;
        }

        public async Task<bool> MoveToCoordinateAsync(double latitude, double longitude, double altitude)
        {
            var parameters = new Dictionary<string, object>
            {
                ["latitude"] = latitude,
                ["longitude"] = longitude,
                ["altitude"] = altitude
            };

            // In a real implementation, this would send precise movement commands
            var success = await _networkService.SendCommandToDroneAsync("MoveTo", parameters);
            
            if (success)
            {
                // Simulate gradual movement
                _latitude = latitude;
                _longitude = longitude;
                _altitude = altitude;
                NotifyStatusChange();
            }

            return success;
        }

        public DroneStatus GetCurrentStatus()
        {
            return new DroneStatus
            {
                Id = "Drone-001",
                IsActive = _isFlying,
                CurrentZone = _currentZone,
                BatteryLevel = _batteryLevel,
                Altitude = _altitude,
                CameraAngle = "0°"
            };
        }

        private void NotifyStatusChange()
        {
            DroneStatusChanged?.Invoke(this, new DroneStatusEventArgs
            {
                Altitude = _altitude,
                Latitude = _latitude,
                Longitude = _longitude,
                BatteryLevel = _batteryLevel,
                Speed = _speed,
                FlightMode = _flightMode,
                IsFlying = _isFlying,
                IsRecording = _isRecording,
                CurrentZone = _currentZone,
                Timestamp = DateTime.Now
            });
        }

        public void Dispose()
        {
            _flightDataUpdater?.Dispose();
        }
    }

    public class GPSCoordinate
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public string Zone { get; set; } = "";
    }

    public class DroneStatusEventArgs : EventArgs
    {
        public double Altitude { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double BatteryLevel { get; set; }
        public double Speed { get; set; }
        public DroneFlightMode FlightMode { get; set; }
        public bool IsFlying { get; set; }
        public bool IsRecording { get; set; }
        public string CurrentZone { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
