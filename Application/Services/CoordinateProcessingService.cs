using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
}
