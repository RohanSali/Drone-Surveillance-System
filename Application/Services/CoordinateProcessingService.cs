using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using DroneSurveillanceSystem.Views;

namespace DroneSurveillanceSystem.Services
{
    /// <summary>
    /// Service for processing alert coordinates using a Python script
    /// </summary>
    public class CoordinateProcessingService
    {
        private static readonly Lazy<CoordinateProcessingService> _instance = 
            new Lazy<CoordinateProcessingService>(() => new CoordinateProcessingService());
        
        public static CoordinateProcessingService Instance => _instance.Value;

        // Path to the Python script - adjust this path as needed
        private readonly string _pythonScriptPath;
        private readonly string _pythonExecutable;

        private CoordinateProcessingService()
        {
            // Get the application base directory
            var appBaseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Default Python script path: Scripts/process_coordinates.py in the application directory
            // You can change this path to match where you place your script
            _pythonScriptPath = Path.Combine(appBaseDir, "Scripts", "process_coordinates.py");
            
            // Try to find Python executable
            // First try "python", then "python3", then check common locations
            _pythonExecutable = FindPythonExecutable();
        }

        /// <summary>
        /// Finds the Python executable on the system
        /// </summary>
        private string FindPythonExecutable()
        {
            // Try common Python executable names
            var pythonNames = new[] { "python", "python3", "py" };
            
            foreach (var pythonName in pythonNames)
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pythonName,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    process.Start();
                    process.WaitForExit(2000);
                    
                    if (process.ExitCode == 0)
                    {
                        return pythonName;
                    }
                }
                catch
                {
                    // Continue to next option
                }
            }
            
            // Default fallback
            return "python";
        }

        /// <summary>
        /// Processes coordinates using the Python script
        /// </summary>
        /// <param name="latitude">Latitude coordinate</param>
        /// <param name="longitude">Longitude coordinate</param>
        /// <param name="altitude">Altitude coordinate</param>
        /// <param name="alertId">Optional alert ID for context</param>
        /// <param name="droneId">Optional drone ID for context</param>
        /// <returns>Processed coordinates as Tuple (lat, lon, alt) or null if processing fails</returns>
        public async Task<Tuple<double, double, double>?> ProcessCoordinatesAsync(
            double latitude, 
            double longitude, 
            double altitude,
            string? alertId = null,
            string? droneId = null)
        {
            try
            {
                // Check if Python script exists
                if (!File.Exists(_pythonScriptPath))
                {
                    Console.WriteLine($"[CoordinateProcessing] Python script not found at: {_pythonScriptPath}");
                    Console.WriteLine($"[CoordinateProcessing] Please place your Python script at: {_pythonScriptPath}");
                    return null;
                }

                // Prepare input data as JSON
                var inputData = new
                {
                    latitude = latitude,
                    longitude = longitude,
                    altitude = altitude,
                    alert_id = alertId,
                    drone_id = droneId,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                };

                var inputJson = JsonConvert.SerializeObject(inputData);

                // Create process to run Python script
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonExecutable,
                        Arguments = $"\"{_pythonScriptPath}\"",
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = Path.GetDirectoryName(_pythonScriptPath)
                    }
                };

                process.Start();

                // Send input JSON to Python script via stdin
                await process.StandardInput.WriteLineAsync(inputJson);
                process.StandardInput.Close();

                // Read output from Python script
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                // Wait for process to complete (with timeout)
                var completed = await Task.Run(() => process.WaitForExit(10000)); // 10 second timeout

                if (!completed)
                {
                    process.Kill();
                    Console.WriteLine("[CoordinateProcessing] Python script execution timed out");
                    return null;
                }

                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"[CoordinateProcessing] Python script exited with code {process.ExitCode}");
                    Console.WriteLine($"[CoordinateProcessing] Error: {error}");
                    return null;
                }

                // Parse output JSON
                if (string.IsNullOrWhiteSpace(output))
                {
                    Console.WriteLine("[CoordinateProcessing] Python script returned empty output");
                    return null;
                }

                // Try to parse the output
                // Expected format: JSON with latitude, longitude, altitude fields
                // Example: {"latitude": 37.7749, "longitude": -122.4194, "altitude": 50.0}
                try
                {
                    var result = JsonConvert.DeserializeObject<CoordinateResult>(output.Trim());
                    
                    if (result != null && 
                        result.Latitude.HasValue && 
                        result.Longitude.HasValue && 
                        result.Altitude.HasValue)
                    {
                        Console.WriteLine($"[CoordinateProcessing] Successfully processed coordinates: [{result.Latitude}, {result.Longitude}, {result.Altitude}]");
                        return new Tuple<double, double, double>(
                            result.Latitude.Value,
                            result.Longitude.Value,
                            result.Altitude.Value
                        );
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[CoordinateProcessing] Failed to parse Python script output: {ex.Message}");
                    Console.WriteLine($"[CoordinateProcessing] Raw output: {output}");
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CoordinateProcessing] Error processing coordinates: {ex.Message}");
                Console.WriteLine($"[CoordinateProcessing] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Gets the expected path where the Python script should be placed
        /// </summary>
        public string GetScriptPath()
        {
            return _pythonScriptPath;
        }

        /// <summary>
        /// Checks if the Python script exists at the expected location
        /// </summary>
        public bool ScriptExists()
        {
            return File.Exists(_pythonScriptPath);
        }

        /// <summary>
        /// Dummy RL module: computes a validation midpoint from local cache data.
        ///
        /// Cache sources:
        /// - AlertManager.Instance.ActiveAlerts (alerts, alert_id, alert_location, drone_id)
        /// - DroneTrackingService.Instance.GetDronePosition(drone_id) (drone_position)
        /// </summary>
        public Task<TargetPosMessage?> ComputeValidationMidpointFromCacheAsync(
            string? alertId,
            string? droneId,
            Tuple<double, double, double>? fallbackAlertLocation = null,
            string? serverAlertId = null,
            string? serverAlertName = null)
        {
            try
            {
                var activeAlerts = AlertManager.Instance.ActiveAlerts;
                AlertData? alert = null;

                if (!string.IsNullOrWhiteSpace(alertId))
                {
                    alert = activeAlerts.FirstOrDefault(a =>
                        !string.IsNullOrWhiteSpace(a.AlertId) &&
                        string.Equals(a.AlertId, alertId, StringComparison.OrdinalIgnoreCase));
                }

                if (alert == null && !string.IsNullOrWhiteSpace(droneId))
                {
                    alert = activeAlerts
                        .Where(a => !string.IsNullOrWhiteSpace(a.DroneId) &&
                                    string.Equals(a.DroneId, droneId, StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(a =>
                        {
                            if (DateTime.TryParse(a.Timestamp, out var parsed)) return parsed;
                            return DateTime.MinValue;
                        })
                        .FirstOrDefault();
                }

                var effectiveAlertLocation = alert?.AlertLocation ?? fallbackAlertLocation;
                if (effectiveAlertLocation == null)
                {
                    Console.WriteLine("[DummyRL] Missing alert_location from local cache.");
                    return Task.FromResult<TargetPosMessage?>(null);
                }

                var effectiveDroneId = !string.IsNullOrWhiteSpace(droneId)
                    ? droneId!
                    : (alert?.DroneId ?? string.Empty);

                if (string.IsNullOrWhiteSpace(effectiveDroneId))
                {
                    Console.WriteLine("[DummyRL] Missing drone_id for local cache lookup.");
                    return Task.FromResult<TargetPosMessage?>(null);
                }

                var dronePosition = DroneTrackingService.Instance.GetDronePosition(effectiveDroneId);
                if (dronePosition == null)
                {
                    Console.WriteLine($"[DummyRL] Missing drone_position in local cache for {effectiveDroneId}.");
                    return Task.FromResult<TargetPosMessage?>(null);
                }

                // midpoint = [(x1+x2)/2, (y1+y2)/2, (z1+z2)/2]
                var midX = (dronePosition.Latitude + effectiveAlertLocation.Item1) / 2.0;
                var midY = (dronePosition.Longitude + effectiveAlertLocation.Item2) / 2.0;
                var midZ = (dronePosition.Altitude + effectiveAlertLocation.Item3) / 2.0;
                var yawOut = dronePosition.Yaw;

                var resolvedAlertId = !string.IsNullOrWhiteSpace(serverAlertId)
                    ? serverAlertId!
                    : (!string.IsNullOrWhiteSpace(alert?.AlertId) ? alert.AlertId! : (alertId ?? string.Empty));
                var resolvedAlertName = serverAlertName ?? alert?.Alert ?? "Unknown Alert";

                var data = new DummyRlCoordinateOutput
                {
                    TargetId = $"target_{Guid.NewGuid():N}",
                    Location = new[] { midX, midY, midZ, yawOut },
                    AlertName = resolvedAlertName,
                    AlertId = resolvedAlertId,
                    DroneId = effectiveDroneId
                };

                var output = new TargetPosMessage
                {
                    Type = "target_pos",
                    Data = data
                };

                Console.WriteLine(
                    $"[DummyRL] midpoint computed | alert_id={data.AlertId} drone_id={data.DroneId} " +
                    $"location=[{data.Location[0]:F6}, {data.Location[1]:F6}, {data.Location[2]:F2}, {data.Location[3]:F2}]");

                return Task.FromResult<TargetPosMessage?>(output);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DummyRL] midpoint processing failed: {ex.Message}");
                return Task.FromResult<TargetPosMessage?>(null);
            }
        }

        /// <summary>
        /// Result model for Python script output
        /// </summary>
        private class CoordinateResult
        {
            [JsonProperty("latitude")]
            public double? Latitude { get; set; }

            [JsonProperty("longitude")]
            public double? Longitude { get; set; }

            [JsonProperty("altitude")]
            public double? Altitude { get; set; }
        }
    }

    public class DummyRlCoordinateOutput
    {
        [JsonProperty("target_id")]
        public string TargetId { get; set; } = string.Empty;

        [JsonProperty("location")]
        public double[] Location { get; set; } = Array.Empty<double>();

        [JsonProperty("alert_name")]
        public string AlertName { get; set; } = string.Empty;

        [JsonProperty("alert_id")]
        public string AlertId { get; set; } = string.Empty;

        [JsonProperty("drone_id")]
        public string DroneId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Message wrapper for target position with message type
    /// </summary>
    public class TargetPosMessage
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "target_pos";

        [JsonProperty("data")]
        public DummyRlCoordinateOutput? Data { get; set; }
    }
}
