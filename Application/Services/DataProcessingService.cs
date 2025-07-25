using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using DroneSurveillanceSystem.Models;
using Newtonsoft.Json;

namespace DroneSurveillanceSystem.Services
{
    public enum DataType
    {
        TelemetryData,
        SensorData,
        ImageData,
        VideoData,
        GPSData,
        BatteryData,
        NetworkData,
        WeatherData
    }

    public class DataProcessingService
    {
        private readonly Queue<DataPacket> _dataQueue = new();
        private readonly Timer _processingTimer;
        private readonly object _queueLock = new object();
        private readonly Random _random = new Random();

        // Processing statistics
        private int _packetsProcessed = 0;
        private int _packetsDropped = 0;
        private double _averageProcessingTime = 0.0;
        private DateTime _lastProcessingTime = DateTime.Now;

        public event EventHandler<DataProcessedEventArgs>? DataProcessed;
        public event EventHandler<string>? ProcessingAlert;

        // Properties
        public int QueueSize => _dataQueue.Count;
        public int PacketsProcessed => _packetsProcessed;
        public int PacketsDropped => _packetsDropped;
        public double AverageProcessingTime => _averageProcessingTime;
        public bool IsProcessing { get; private set; } = true;

        public DataProcessingService()
        {
            // Process data every 100ms for real-time performance
            _processingTimer = new Timer(ProcessDataQueue, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }

        public void EnqueueData(DataPacket packet)
        {
            lock (_queueLock)
            {
                // Prevent queue overflow
                if (_dataQueue.Count > 1000)
                {
                    _dataQueue.Dequeue(); // Drop oldest packet
                    _packetsDropped++;
                    ProcessingAlert?.Invoke(this, "Data queue overflow - dropping oldest packets");
                }

                _dataQueue.Enqueue(packet);
            }
        }

        private void ProcessDataQueue(object? state)
        {
            if (!IsProcessing) return;

            List<DataPacket> packetsToProcess;
            
            lock (_queueLock)
            {
                if (_dataQueue.Count == 0) return;

                // Process up to 10 packets per cycle
                packetsToProcess = new List<DataPacket>();
                for (int i = 0; i < Math.Min(10, _dataQueue.Count); i++)
                {
                    packetsToProcess.Add(_dataQueue.Dequeue());
                }
            }

            foreach (var packet in packetsToProcess)
            {
                ProcessDataPacket(packet);
            }
        }

        private void ProcessDataPacket(DataPacket packet)
        {
            var startTime = DateTime.Now;

            try
            {
                var processedData = packet.Type switch
                {
                    DataType.TelemetryData => ProcessTelemetryData(packet),
                    DataType.SensorData => ProcessSensorData(packet),
                    DataType.ImageData => ProcessImageData(packet),
                    DataType.VideoData => ProcessVideoData(packet),
                    DataType.GPSData => ProcessGPSData(packet),
                    DataType.BatteryData => ProcessBatteryData(packet),
                    DataType.NetworkData => ProcessNetworkData(packet),
                    DataType.WeatherData => ProcessWeatherData(packet),
                    _ => ProcessGenericData(packet)
                };

                _packetsProcessed++;
                
                // Calculate processing time
                var processingTime = (DateTime.Now - startTime).TotalMilliseconds;
                _averageProcessingTime = (_averageProcessingTime * (_packetsProcessed - 1) + processingTime) / _packetsProcessed;
                _lastProcessingTime = DateTime.Now;

                // Notify listeners
                DataProcessed?.Invoke(this, new DataProcessedEventArgs
                {
                    OriginalPacket = packet,
                    ProcessedData = processedData,
                    ProcessingTime = TimeSpan.FromMilliseconds(processingTime),
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                ProcessingAlert?.Invoke(this, $"Error processing packet {packet.Id}: {ex.Message}");
            }
        }

        private ProcessedData ProcessTelemetryData(DataPacket packet)
        {
            var telemetryData = JsonConvert.DeserializeObject<TelemetryData>(packet.Data);
            
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "TelemetryProcessed",
                Data = new
                {
                    Altitude = telemetryData?.Altitude ?? 0,
                    Speed = telemetryData?.Speed ?? 0,
                    BatteryLevel = telemetryData?.BatteryLevel ?? 0,
                    Temperature = telemetryData?.Temperature ?? 0,
                    Humidity = telemetryData?.Humidity ?? 0,
                    ProcessedAt = DateTime.Now,
                    Status = DetermineTelemetryStatus(telemetryData)
                },
                Priority = DeterminePriority(packet),
                Alerts = GenerateAlertsForTelemetry(telemetryData)
            };
        }

        private ProcessedData ProcessSensorData(DataPacket packet)
        {
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "SensorProcessed",
                Data = new
                {
                    SensorReadings = ProcessSensorReadings(packet.Data),
                    AnomaliesDetected = DetectAnomalies(packet.Data),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = new List<string>()
            };
        }

        private ProcessedData ProcessImageData(DataPacket packet)
        {
            // Simulate image processing
            Task.Delay(50); // Simulate processing time

            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "ImageProcessed",
                Data = new
                {
                    ImageSize = packet.Data.Length,
                    ObjectsDetected = _random.Next(0, 15),
                    QualityScore = _random.NextDouble(),
                    ProcessedAt = DateTime.Now,
                    ThumbnailGenerated = true
                },
                Priority = DeterminePriority(packet),
                Alerts = new List<string>()
            };
        }

        private ProcessedData ProcessVideoData(DataPacket packet)
        {
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "VideoProcessed",
                Data = new
                {
                    FrameCount = _random.Next(30, 120),
                    Duration = _random.Next(1, 10),
                    Bitrate = _random.Next(1000, 5000),
                    ProcessedFrames = _random.Next(30, 120),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = new List<string>()
            };
        }

        private ProcessedData ProcessGPSData(DataPacket packet)
        {
            var gpsData = JsonConvert.DeserializeObject<GPSData>(packet.Data);
            
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "GPSProcessed",
                Data = new
                {
                    Latitude = gpsData?.Latitude ?? 0,
                    Longitude = gpsData?.Longitude ?? 0,
                    Altitude = gpsData?.Altitude ?? 0,
                    Accuracy = gpsData?.Accuracy ?? 0,
                    SatelliteCount = gpsData?.SatelliteCount ?? 0,
                    Zone = DetermineZone(gpsData?.Latitude ?? 0, gpsData?.Longitude ?? 0),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = GenerateGPSAlerts(gpsData)
            };
        }

        private ProcessedData ProcessBatteryData(DataPacket packet)
        {
            var batteryData = JsonConvert.DeserializeObject<BatteryData>(packet.Data);
            
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "BatteryProcessed",
                Data = new
                {
                    Level = batteryData?.Level ?? 0,
                    Voltage = batteryData?.Voltage ?? 0,
                    Current = batteryData?.Current ?? 0,
                    Temperature = batteryData?.Temperature ?? 0,
                    Health = DetermineBatteryHealth(batteryData),
                    EstimatedTimeRemaining = CalculateTimeRemaining(batteryData),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = GenerateBatteryAlerts(batteryData)
            };
        }

        private ProcessedData ProcessNetworkData(DataPacket packet)
        {
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "NetworkProcessed",
                Data = new
                {
                    SignalStrength = _random.Next(50, 100),
                    Latency = _random.Next(10, 100),
                    PacketLoss = _random.NextDouble() * 0.05,
                    Bandwidth = _random.Next(1000, 10000),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = new List<string>()
            };
        }

        private ProcessedData ProcessWeatherData(DataPacket packet)
        {
            var weatherData = JsonConvert.DeserializeObject<WeatherData>(packet.Data);
            
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "WeatherProcessed",
                Data = new
                {
                    Temperature = weatherData?.Temperature ?? 0,
                    Humidity = weatherData?.Humidity ?? 0,
                    WindSpeed = weatherData?.WindSpeed ?? 0,
                    WindDirection = weatherData?.WindDirection ?? 0,
                    Pressure = weatherData?.Pressure ?? 0,
                    FlightSuitability = DetermineFlightSuitability(weatherData),
                    ProcessedAt = DateTime.Now
                },
                Priority = DeterminePriority(packet),
                Alerts = GenerateWeatherAlerts(weatherData)
            };
        }

        private ProcessedData ProcessGenericData(DataPacket packet)
        {
            return new ProcessedData
            {
                Id = Guid.NewGuid().ToString(),
                Type = "GenericProcessed",
                Data = new
                {
                    Size = packet.Data.Length,
                    ProcessedAt = DateTime.Now
                },
                Priority = Priority.Low,
                Alerts = new List<string>()
            };
        }

        private string DetermineTelemetryStatus(TelemetryData? data)
        {
            if (data == null) return "Unknown";
            
            if (data.BatteryLevel < 20) return "Critical";
            if (data.Altitude > 100) return "High Altitude";
            if (data.Speed > 50) return "High Speed";
            
            return "Normal";
        }

        private Priority DeterminePriority(DataPacket packet)
        {
            return packet.Type switch
            {
                DataType.BatteryData => Priority.High,
                DataType.GPSData => Priority.High,
                DataType.TelemetryData => Priority.Medium,
                DataType.SensorData => Priority.Medium,
                DataType.ImageData => Priority.Low,
                DataType.VideoData => Priority.Low,
                _ => Priority.Low
            };
        }

        private List<string> GenerateAlertsForTelemetry(TelemetryData? data)
        {
            var alerts = new List<string>();
            
            if (data == null) return alerts;
            
            if (data.BatteryLevel < 20)
                alerts.Add("Low battery warning");
            
            if (data.Altitude > 100)
                alerts.Add("Maximum altitude exceeded");
            
            if (data.Temperature > 60)
                alerts.Add("High temperature detected");
            
            return alerts;
        }

        private List<string> GenerateGPSAlerts(GPSData? data)
        {
            var alerts = new List<string>();
            
            if (data == null) return alerts;
            
            if (data.Accuracy > 10)
                alerts.Add("GPS accuracy degraded");
            
            if (data.SatelliteCount < 4)
                alerts.Add("Insufficient GPS satellites");
            
            return alerts;
        }

        private List<string> GenerateBatteryAlerts(BatteryData? data)
        {
            var alerts = new List<string>();
            
            if (data == null) return alerts;
            
            if (data.Level < 15)
                alerts.Add("Critical battery level");
            else if (data.Level < 30)
                alerts.Add("Low battery level");
            
            if (data.Temperature > 45)
                alerts.Add("Battery overheating");
            
            return alerts;
        }

        private List<string> GenerateWeatherAlerts(WeatherData? data)
        {
            var alerts = new List<string>();
            
            if (data == null) return alerts;
            
            if (data.WindSpeed > 15)
                alerts.Add("High wind conditions");
            
            if (data.Temperature < 0 || data.Temperature > 40)
                alerts.Add("Extreme temperature conditions");
            
            return alerts;
        }

        private string DetermineZone(double latitude, double longitude)
        {
            // Simple zone determination based on coordinates
            var zones = new[] { "Zone-A", "Zone-B", "Zone-C", "Zone-D" };
            var hash = Math.Abs((latitude + longitude).GetHashCode());
            return zones[hash % zones.Length];
        }

        private string DetermineBatteryHealth(BatteryData? data)
        {
            if (data == null) return "Unknown";
            
            if (data.Level > 80 && data.Temperature < 35) return "Excellent";
            if (data.Level > 60 && data.Temperature < 40) return "Good";
            if (data.Level > 30 && data.Temperature < 50) return "Fair";
            
            return "Poor";
        }

        private int CalculateTimeRemaining(BatteryData? data)
        {
            if (data == null || data.Current <= 0) return 0;
            
            // Simple calculation: minutes = (battery_level * capacity) / current_draw
            var capacity = 5000; // mAh
            return (int)((data.Level / 100.0 * capacity) / data.Current * 60);
        }

        private string DetermineFlightSuitability(WeatherData? data)
        {
            if (data == null) return "Unknown";
            
            if (data.WindSpeed > 20 || data.Temperature < -10 || data.Temperature > 45)
                return "Unsuitable";
            
            if (data.WindSpeed > 15 || data.Temperature < 0 || data.Temperature > 35)
                return "Marginal";
            
            return "Suitable";
        }

        private Dictionary<string, object> ProcessSensorReadings(string data)
        {
            return new Dictionary<string, object>
            {
                ["processedSensors"] = _random.Next(5, 15),
                ["averageValue"] = _random.NextDouble() * 100,
                ["maxValue"] = _random.NextDouble() * 150,
                ["minValue"] = _random.NextDouble() * 50
            };
        }

        private List<string> DetectAnomalies(string data)
        {
            var anomalies = new List<string>();
            
            if (_random.NextDouble() < 0.1)
                anomalies.Add("Sensor drift detected");
            
            if (_random.NextDouble() < 0.05)
                anomalies.Add("Sensor calibration required");
            
            return anomalies;
        }

        public void StartProcessing()
        {
            IsProcessing = true;
        }

        public void StopProcessing()
        {
            IsProcessing = false;
        }

        public ProcessingStatistics GetStatistics()
        {
            return new ProcessingStatistics
            {
                PacketsProcessed = _packetsProcessed,
                PacketsDropped = _packetsDropped,
                QueueSize = QueueSize,
                AverageProcessingTime = _averageProcessingTime,
                LastProcessingTime = _lastProcessingTime,
                IsProcessing = IsProcessing
            };
        }

        public void Dispose()
        {
            _processingTimer?.Dispose();
        }
    }

    // Data models
    public class DataPacket
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DataType Type { get; set; }
        public string Data { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Source { get; set; } = "";
        public Priority Priority { get; set; } = Priority.Medium;
    }

    public class ProcessedData
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public object Data { get; set; } = new object();
        public Priority Priority { get; set; } = Priority.Medium;
        public List<string> Alerts { get; set; } = new();
        public DateTime ProcessedAt { get; set; } = DateTime.Now;
    }

    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class ProcessingStatistics
    {
        public int PacketsProcessed { get; set; }
        public int PacketsDropped { get; set; }
        public int QueueSize { get; set; }
        public double AverageProcessingTime { get; set; }
        public DateTime LastProcessingTime { get; set; }
        public bool IsProcessing { get; set; }
    }

    public class DataProcessedEventArgs : EventArgs
    {
        public DataPacket OriginalPacket { get; set; } = new();
        public ProcessedData ProcessedData { get; set; } = new();
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; }
    }

    // Specific data models
    public class TelemetryData
    {
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double BatteryLevel { get; set; }
        public double Temperature { get; set; }
        public double Humidity { get; set; }
    }

    public class GPSData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Accuracy { get; set; }
        public int SatelliteCount { get; set; }
    }

    public class BatteryData
    {
        public double Level { get; set; }
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Temperature { get; set; }
    }

    public class WeatherData
    {
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public double WindSpeed { get; set; }
        public double WindDirection { get; set; }
        public double Pressure { get; set; }
    }
}
