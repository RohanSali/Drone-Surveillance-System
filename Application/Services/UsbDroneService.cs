using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services
{
    public class UsbDrone
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Ready for Module Installation";
        public string UsbPort { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public bool IsConnected { get; set; } = true;
        public DateTime ConnectedTime { get; set; } = DateTime.Now;
        public string DroneType { get; set; } = "Surveillance";
        public string FirmwareVersion { get; set; } = "v2.1.0";
        public int BatteryLevel { get; set; } = 85;
    }

    public class UsbDroneService
    {
        private List<UsbDrone> _connectedDrones = new List<UsbDrone>();
        private readonly object _lockObject = new object();

        public event EventHandler<List<UsbDrone>>? DronesListChanged;

        public List<UsbDrone> GetConnectedDrones()
        {
            lock (_lockObject)
            {
                return _connectedDrones.ToList();
            }
        }

        public async Task<List<UsbDrone>> DetectUsbDronesAsync()
        {
            return await Task.Run(() =>
            {
                var detectedDrones = new List<UsbDrone>();
                
                try
                {
                    // Simulate USB drone detection
                    // In a real implementation, this would scan for actual USB devices
                    // var availablePorts = SerialPort.GetPortNames(); // Removed for compatibility
                    
                    // Create sample drones for demonstration
                    var sampleDrones = new[]
                    {
                        new UsbDrone 
                        { 
                            Name = "Drone_Alpha_1", 
                            UsbPort = "COM3", 
                            DeviceId = "USB_DRONE_001",
                            DroneType = "Surveillance",
                            FirmwareVersion = "v2.1.0",
                            BatteryLevel = 92
                        },
                        new UsbDrone 
                        { 
                            Name = "Drone_Beta_2", 
                            UsbPort = "COM5", 
                            DeviceId = "USB_DRONE_002",
                            DroneType = "Patrol",
                            FirmwareVersion = "v2.0.8",
                            BatteryLevel = 78
                        },
                        new UsbDrone 
                        { 
                            Name = "Drone_Gamma_3", 
                            UsbPort = "COM7", 
                            DeviceId = "USB_DRONE_003",
                            DroneType = "Surveillance",
                            FirmwareVersion = "v2.1.2",
                            BatteryLevel = 85
                        },
                        new UsbDrone 
                        { 
                            Name = "Drone_Theta_4", 
                            UsbPort = "COM9", 
                            DeviceId = "USB_DRONE_004",
                            DroneType = "Patrol",
                            FirmwareVersion = "v2.0.9",
                            BatteryLevel = 67
                        }
                    };

                    detectedDrones.AddRange(sampleDrones);
                }
                catch (Exception ex)
                {
                    // Log error in real implementation
                    Console.WriteLine($"Error detecting USB drones: {ex.Message}");
                }

                lock (_lockObject)
                {
                    _connectedDrones = detectedDrones;
                }

                DronesListChanged?.Invoke(this, detectedDrones);
                return detectedDrones;
            });
        }

        public async Task<bool> ConnectToDroneAsync(string droneName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var drone = _connectedDrones.FirstOrDefault(d => d.Name == droneName);
                    if (drone != null)
                    {
                        drone.Status = "Connected - Ready for Operations";
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting to drone {droneName}: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> DisconnectDroneAsync(string droneName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var drone = _connectedDrones.FirstOrDefault(d => d.Name == droneName);
                    if (drone != null)
                    {
                        drone.Status = "Disconnected";
                        drone.IsConnected = false;
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disconnecting drone {droneName}: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> InstallModuleAsync(string droneName, string moduleName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Simulate module installation
                    Task.Delay(2000).Wait(); // Simulate installation time
                    
                    var drone = _connectedDrones.FirstOrDefault(d => d.Name == droneName);
                    if (drone != null)
                    {
                        drone.Status = $"Module {moduleName} Installed Successfully";
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error installing module on drone {droneName}: {ex.Message}");
                    return false;
                }
            });
        }

        public async Task<bool> FetchDataAsync(string droneName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Simulate data fetching
                    Task.Delay(1500).Wait(); // Simulate fetch time
                    
                    var drone = _connectedDrones.FirstOrDefault(d => d.Name == droneName);
                    if (drone != null)
                    {
                        drone.Status = "Data Fetched Successfully";
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching data from drone {droneName}: {ex.Message}");
                    return false;
                }
            });
        }

        public UsbDrone? GetDroneByName(string droneName)
        {
            lock (_lockObject)
            {
                return _connectedDrones.FirstOrDefault(d => d.Name == droneName);
            }
        }
    }
} 