using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using DroneSurveillanceSystem.Models;
using System.Data.SQLite;
using System.Linq;

namespace DroneSurveillanceSystem.Services
{
    public class SurveillanceService
    {
        private readonly string _dataPath;
        private readonly string _jsonLogPath;
        private readonly string _databasePath;
        private readonly List<DetectionEvent> _detectionHistory;

        public SurveillanceService()
        {
            _dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillance");
            _jsonLogPath = Path.Combine(_dataPath, "detection_log.json");
            _databasePath = Path.Combine(_dataPath, "surveillance.db");
            _detectionHistory = new List<DetectionEvent>();

            InitializeDataStorage();
        }

        private void InitializeDataStorage()
        {
            // Create data directory if it doesn't exist
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            // Initialize SQLite database
            InitializeDatabase();

            // Load existing JSON data if available
            LoadJsonData();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS DetectionEvents (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp DATETIME NOT NULL,
                        Zone TEXT NOT NULL,
                        Status TEXT NOT NULL,
                        DroneId TEXT NOT NULL,
                        Latitude REAL NOT NULL,
                        Longitude REAL NOT NULL,
                        CrowdCount INTEGER NOT NULL,
                        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
                    )";

                using var command = new SQLiteCommand(createTableQuery, connection);
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database initialization error: {ex.Message}");
            }
        }

        public async Task<bool> LogDetectionEventAsync(DetectionEvent detectionEvent)
        {
            try
            {
                // Add to memory collection
                _detectionHistory.Add(detectionEvent);

                // Save to JSON file
                await SaveToJsonAsync();

                // Save to SQLite database
                await SaveToDatabaseAsync(detectionEvent);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging detection event: {ex.Message}");
                return false;
            }
        }

        private async Task SaveToJsonAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_detectionHistory, Formatting.Indented);
                await File.WriteAllTextAsync(_jsonLogPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to JSON: {ex.Message}");
            }
        }

        private async Task SaveToDatabaseAsync(DetectionEvent detectionEvent)
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                await connection.OpenAsync();

                string insertQuery = @"
                    INSERT INTO DetectionEvents (Timestamp, Zone, Status, DroneId, Latitude, Longitude, CrowdCount)
                    VALUES (@timestamp, @zone, @status, @droneId, @latitude, @longitude, @crowdCount)";

                using var command = new SQLiteCommand(insertQuery, connection);
                command.Parameters.AddWithValue("@timestamp", detectionEvent.Timestamp);
                command.Parameters.AddWithValue("@zone", detectionEvent.Zone);
                command.Parameters.AddWithValue("@status", detectionEvent.Status);
                command.Parameters.AddWithValue("@droneId", detectionEvent.DroneId);
                command.Parameters.AddWithValue("@latitude", detectionEvent.Latitude);
                command.Parameters.AddWithValue("@longitude", detectionEvent.Longitude);
                command.Parameters.AddWithValue("@crowdCount", detectionEvent.CrowdCount);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving to database: {ex.Message}");
            }
        }

        private void LoadJsonData()
        {
            try
            {
                if (File.Exists(_jsonLogPath))
                {
                    var json = File.ReadAllText(_jsonLogPath);
                    var loadedEvents = JsonConvert.DeserializeObject<List<DetectionEvent>>(json);
                    if (loadedEvents != null)
                    {
                        _detectionHistory.AddRange(loadedEvents);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading JSON data: {ex.Message}");
            }
        }

        public async Task<List<DetectionEvent>> GetDetectionHistoryAsync(int limit = 100)
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                await connection.OpenAsync();

                string selectQuery = @"
                    SELECT Timestamp, Zone, Status, DroneId, Latitude, Longitude, CrowdCount
                    FROM DetectionEvents
                    ORDER BY Timestamp DESC
                    LIMIT @limit";

                using var command = new SQLiteCommand(selectQuery, connection);
                command.Parameters.AddWithValue("@limit", limit);

                var events = new List<DetectionEvent>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    events.Add(new DetectionEvent
                    {
                        Timestamp = reader.GetDateTime(0),
                        Zone = reader.GetString(1),
                        Status = reader.GetString(2),
                        DroneId = reader.GetString(3),
                        Latitude = reader.GetDouble(4),
                        Longitude = reader.GetDouble(5),
                        CrowdCount = reader.GetInt32(6)
                    });
                }

                return events;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving detection history: {ex.Message}");
                return new List<DetectionEvent>();
            }
        }

        // Simulated AI detection method (placeholder for real ONNX/ML implementation)
        public SurveillanceResult AnalyzeImage(string imagePath)
        {
            try
            {
                // In a real implementation, this would use ONNX model or other AI framework
                // For simulation, we'll return random results

                var random = new Random();
                bool crowdDetected = random.Next(1, 100) <= 20; // 20% chance

                return new SurveillanceResult
                {
                    CrowdDetected = crowdDetected,
                    PeopleCount = crowdDetected ? random.Next(1, 30) : 0,
                    Confidence = random.NextDouble() * 0.3 + 0.7, // 70-100% confidence
                    ProcessingTime = TimeSpan.FromMilliseconds(random.Next(50, 200)),
                    BoundingBoxes = crowdDetected ? GenerateRandomBoundingBoxes(random.Next(1, 5)) : new List<SurveillanceBoundingBox>()
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing image: {ex.Message}");
                return new SurveillanceResult
                {
                    CrowdDetected = false,
                    PeopleCount = 0,
                    Confidence = 0,
                    ProcessingTime = TimeSpan.Zero,
                    BoundingBoxes = new List<SurveillanceBoundingBox>()
                };
            }
        }

        private List<SurveillanceBoundingBox> GenerateRandomBoundingBoxes(int count)
        {
            var random = new Random();
            var boxes = new List<SurveillanceBoundingBox>();

            for (int i = 0; i < count; i++)
            {
                boxes.Add(new SurveillanceBoundingBox
                {
                    X = random.Next(0, 800),
                    Y = random.Next(0, 600),
                    Width = random.Next(50, 150),
                    Height = random.Next(100, 200),
                    Confidence = random.NextDouble() * 0.3 + 0.7
                });
            }

            return boxes;
        }

        public async Task<string?> ExportDetectionDataAsync(DateTime from, DateTime to, string format = "json")
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                await connection.OpenAsync();

                string selectQuery = @"
                    SELECT Timestamp, Zone, Status, DroneId, Latitude, Longitude, CrowdCount
                    FROM DetectionEvents
                    WHERE Timestamp BETWEEN @from AND @to
                    ORDER BY Timestamp DESC";

                using var command = new SQLiteCommand(selectQuery, connection);
                command.Parameters.AddWithValue("@from", from);
                command.Parameters.AddWithValue("@to", to);

                var events = new List<DetectionEvent>();
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    events.Add(new DetectionEvent
                    {
                        Timestamp = reader.GetDateTime(0),
                        Zone = reader.GetString(1),
                        Status = reader.GetString(2),
                        DroneId = reader.GetString(3),
                        Latitude = reader.GetDouble(4),
                        Longitude = reader.GetDouble(5),
                        CrowdCount = reader.GetInt32(6)
                    });
                }

                string exportPath = Path.Combine(_dataPath, $"export_{DateTime.Now:yyyyMMdd_HHmmss}.{format}");

                if (format.ToLower() == "json")
                {
                    var json = JsonConvert.SerializeObject(events, Formatting.Indented);
                    await File.WriteAllTextAsync(exportPath, json);
                }
                else if (format.ToLower() == "csv")
                {
                    var csv = "Timestamp,Zone,Status,DroneId,Latitude,Longitude,CrowdCount\n";
                    foreach (var evt in events)
                    {
                        csv += $"{evt.Timestamp:yyyy-MM-dd HH:mm:ss},{evt.Zone},{evt.Status},{evt.DroneId},{evt.Latitude},{evt.Longitude},{evt.CrowdCount}\n";
                    }
                    await File.WriteAllTextAsync(exportPath, csv);
                }

                return exportPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exporting data: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            // Cleanup resources if needed
        }
    }
    
    // SurveillanceResult for AI analysis results
    public class SurveillanceResult
    {
        public bool CrowdDetected { get; set; }
        public int PeopleCount { get; set; }
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public List<SurveillanceBoundingBox> BoundingBoxes { get; set; } = new List<SurveillanceBoundingBox>();
    }
    
    public class SurveillanceBoundingBox
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public double Confidence { get; set; }
    }
}
