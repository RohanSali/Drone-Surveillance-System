using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Websocket.Client;
using Newtonsoft.Json;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Views;
using System.IO;
using System.Linq; // Added for .OfType() and .FirstOrDefault()
using System.Windows; // Added for Window
using System.Threading; // Added for Timer
using System.Net.WebSockets; // Added for WebSocketCloseStatus

namespace DroneSurveillanceSystem.Services
{
    public class AlertReceivedEventArgs : EventArgs
    {
        public AlertData? Alert { get; }
        public AlertReceivedEventArgs(AlertData? alert) => Alert = alert;
    }

    public class ApiService
    {
        private static readonly Lazy<ApiService> _instance = new Lazy<ApiService>(() => new ApiService());
        public static ApiService Instance => _instance.Value;
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _authToken;
        internal WebsocketClient? _client;
        private readonly string _wsUrl = "wss://new-server-5iyd.onrender.com/ws/application/app_001";
        private readonly string _logFilePath;
        private bool _isConnected = false;
        private Timer? _heartbeatTimer;
        private readonly object _lockObject = new object();
        private bool _isStarting = false;
        public event EventHandler<AlertReceivedEventArgs>? AlertReceived;
        public event EventHandler<string>? MessageReceived;

        public ApiService(string baseUrl = "wss://new-server-5iyd.onrender.com", string authToken = "")
        {
            _baseUrl = baseUrl;
            _authToken = authToken;
            _httpClient = new HttpClient();
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logger.txt");
            
            if (!string.IsNullOrEmpty(_authToken))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_authToken}");
            }
            
            // Initialize log file
            InitializeLogFile();
        }

        // Group Management
        public async Task<ApiResponse<GroupCreationResult>> CreateGroupAsync(string region, string purpose, string rlModelInstance)
        {
            try
            {
                var data = new
                {
                    region = region,
                    purpose = purpose,
                    rl_model_instance = rlModelInstance
                };

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/groups/create/", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<GroupCreationResult>(responseContent) ?? new GroupCreationResult();
                    return new ApiResponse<GroupCreationResult> { Success = true, Data = result };
                }
                else
                {
                    return new ApiResponse<GroupCreationResult> { Success = false, ErrorMessage = responseContent };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<GroupCreationResult> { Success = false, ErrorMessage = ex.Message };
            }
        }

        // Drone Registration
        public async Task<ApiResponse<DroneRegistrationResult>> RegisterDroneAsync(int droneId, string location, string purpose)
        {
            try
            {
                var data = new
                {
                    drone_id = droneId,
                    location = location,
                    purpose = purpose
                };

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/drones/register/", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<DroneRegistrationResult>(responseContent) ?? new DroneRegistrationResult();
                    return new ApiResponse<DroneRegistrationResult> { Success = true, Data = result };
                }
                else
                {
                    return new ApiResponse<DroneRegistrationResult> { Success = false, ErrorMessage = responseContent };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<DroneRegistrationResult> { Success = false, ErrorMessage = ex.Message };
            }
        }

        // Data Upload
        public async Task<ApiResponse<DataUploadResult>> UploadDataAsync(int droneId, byte[] imageData, string fileName, string location, double score)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(droneId.ToString()), "drone_id");
                form.Add(new StringContent(location), "location");
                form.Add(new StringContent(score.ToString()), "score");
                form.Add(new ByteArrayContent(imageData), "image", fileName);

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/drones/data/", form);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<DataUploadResult>(responseContent) ?? new DataUploadResult();
                    return new ApiResponse<DataUploadResult> { Success = true, Data = result };
                }
                else
                {
                    return new ApiResponse<DataUploadResult> { Success = false, ErrorMessage = responseContent };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<DataUploadResult> { Success = false, ErrorMessage = ex.Message };
            }
        }

        // Send Control Commands
        public async Task<ApiResponse<ControlCommandResult>> SendControlCommandAsync(int groupId, string command)
        {
            try
            {
                var data = new
                {
                    group_id = groupId,
                    command = command
                };

                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/v1/drones/control/", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonConvert.DeserializeObject<ControlCommandResult>(responseContent) ?? new ControlCommandResult();
                    return new ApiResponse<ControlCommandResult> { Success = true, Data = result };
                }
                else
                {
                    return new ApiResponse<ControlCommandResult> { Success = false, ErrorMessage = responseContent };
                }
            }
            catch (Exception ex)
            {
                return new ApiResponse<ControlCommandResult> { Success = false, ErrorMessage = ex.Message };
            }
        }

        // Check Server Health
        public async Task<ApiResponse<bool>> CheckServerHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/docs");
                return new ApiResponse<bool> { Success = response.IsSuccessStatusCode, Data = response.IsSuccessStatusCode };
            }
            catch (Exception ex)
            {
                return new ApiResponse<bool> { Success = false, Data = false, ErrorMessage = ex.Message };
            }
        }

        public async Task StartWebSocketAsync()
        {
            lock (_lockObject)
            {
                if (_isStarting)
                {
                    LogToFile("INFO", "WebSocket start already in progress, skipping duplicate start");
                    return;
                }
                if (_client != null && _isConnected && _client.IsRunning)
                {
                    LogToFile("INFO", "WebSocket already connected, skipping start");
                    return;
                }
                _isStarting = true;
            }

            try
            {
                var url = new Uri(_wsUrl);
                _client = new WebsocketClient(url);
                
                // Enhanced reconnection settings
                _client.ReconnectTimeout = TimeSpan.FromSeconds(60);
                _client.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);
                
                // Enable automatic reconnection
                _client.ReconnectionHappened.Subscribe(info =>
                {
                    _isConnected = true;
                    LogToFile("RECONNECT", $"Reconnection happened, type: {info.Type}");
                    Console.WriteLine($"[WebSocket] 🔄 Reconnection happened, type: {info.Type}");
                    
                    // Restart heartbeat timer after reconnection
                    StartHeartbeatTimer();
                });
                
                _client.DisconnectionHappened.Subscribe(info =>
                {
                    _isConnected = false;
                    LogToFile("DISCONNECT", $"Disconnection happened, type: {info.Type}, reason: {info.CloseStatusDescription}");
                    Console.WriteLine($"[WebSocket] ❌ Disconnection happened, type: {info.Type}, reason: {info.CloseStatusDescription}");
                    
                    // Stop heartbeat timer on disconnection
                    StopHeartbeatTimer();
                });

                _client.MessageReceived
                    .Where(msg => msg.Text != null)
                    .Subscribe(msg => 
                    {
                        LogToFile("RECEIVED", msg.Text);
                        Console.WriteLine($"[WebSocket] 📨 Message received from server");
                        HandleMessage(msg.Text);
                        
                        // Trigger MessageReceived event for Lost Finding functionality
                        MessageReceived?.Invoke(this, msg.Text);
                    });
                    
                LogToFile("CONNECT", $"Starting WebSocket connection to: {_wsUrl}");
                Console.WriteLine($"[WebSocket] 🚀 Starting WebSocket connection to: {_wsUrl}");
                
                await _client.Start();
                _isConnected = true;
                
                // Start heartbeat timer
                StartHeartbeatTimer();
                
                LogToFile("CONNECT", "WebSocket connection started successfully");
                Console.WriteLine($"[WebSocket] ✅ WebSocket connection started successfully");
            }
            catch (Exception ex)
            {
                _isConnected = false;
                LogToFile("ERROR", $"Failed to start WebSocket connection: {ex.Message}");
                Console.WriteLine($"[WebSocket] ❌ Failed to start WebSocket connection: {ex.Message}");
                throw;
            }
            finally
            {
                lock (_lockObject)
                {
                    _isStarting = false;
                }
            }
        }

        public void HandleMessage(string message)
        {
            Console.WriteLine($"[WebSocket] Received raw message: {message}");
            
            // Handle ping responses
            if (message.Contains("\"type\":\"pong\"") || message.Contains("\"type\":\"ping\""))
            {
                LogToFile("RECEIVED", $"Heartbeat response: {message}");
                return;
            }
            
            try
            {
                var msgObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                if (msgObj != null && msgObj.ContainsKey("type"))
                {
                    var type = msgObj["type"].ToString();
                    Console.WriteLine($"[WebSocket] Message type: {type}");
                    
                    // Log all available keys in the message
                    Console.WriteLine($"[WebSocket] Available keys: {string.Join(", ", msgObj.Keys)}");
                    
                    if (type == "new_alert" && msgObj.ContainsKey("alert"))
                    {
                        // Handle the server's broadcast format to applications
                        var alertJson = msgObj["alert"]?.ToString();
                        if (!string.IsNullOrEmpty(alertJson))
                        {
                            Console.WriteLine($"[WebSocket] Alert JSON: {alertJson}");
                            
                            var alertData = JsonConvert.DeserializeObject<Dictionary<string, object>>(alertJson);
                            if (alertData != null)
                            {
                                Console.WriteLine($"[WebSocket] Alert data keys: {string.Join(", ", alertData.Keys)}");
                                
                                var alert = new AlertData
                                {
                                    Alert = alertData.ContainsKey("alert") ? alertData["alert"]?.ToString() ?? "Unknown Alert" : "Unknown Alert",
                                    DroneId = alertData.ContainsKey("drone_id") ? alertData["drone_id"]?.ToString() ?? "Unknown" : "Unknown",
                                    Score = alertData.ContainsKey("score") ? Convert.ToDouble(alertData["score"]) : 0.0,
                                    RLResponsed = alertData.ContainsKey("rl_responsed") ? Convert.ToInt32(alertData["rl_responsed"]) : 0,
                                    ImageReceived = alertData.ContainsKey("image_received") ? Convert.ToInt32(alertData["image_received"]) : 0,
                                    Image = alertData.ContainsKey("image") ? alertData["image"]?.ToString() : null,
                                    Timestamp = alertData.ContainsKey("timestamp") ? alertData["timestamp"]?.ToString() ?? DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff") : DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                };
                                
                                // Parse alert_location - could be array or string
                                if (alertData.ContainsKey("alert_location") && alertData["alert_location"] != null)
                                {
                                    var locationValue = alertData["alert_location"];
                                    Console.WriteLine($"[WebSocket] Location value type: {locationValue.GetType()}, value: {locationValue}");
                                    
                                    try
                                    {
                                        if (locationValue is Newtonsoft.Json.Linq.JArray locationArray)
                                        {
                                            // It's already a JArray
                                            var coords = locationArray.ToObject<double[]>();
                                            if (coords != null && coords.Length >= 3)
                                            {
                                                alert.AlertLocation = new Tuple<double, double, double>(coords[0], coords[1], coords[2]);
                                                Console.WriteLine($"[WebSocket] Parsed location from JArray: [{coords[0]}, {coords[1]}, {coords[2]}]");
                                            }
                                        }
                                        else
                                        {
                                            // Try to parse as string
                                            var locationStr = locationValue?.ToString();
                                            if (!string.IsNullOrEmpty(locationStr) && locationStr.StartsWith("[") && locationStr.EndsWith("]"))
                                            {
                                                var coords = JsonConvert.DeserializeObject<double[]>(locationStr);
                                                if (coords != null && coords.Length >= 3)
                                                {
                                                    alert.AlertLocation = new Tuple<double, double, double>(coords[0], coords[1], coords[2]);
                                                    Console.WriteLine($"[WebSocket] Parsed location from string: [{coords[0]}, {coords[1]}, {coords[2]}]");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception locEx)
                                    {
                                        Console.WriteLine($"[WebSocket] Location parsing error: {locEx.Message}");
                                    }
                                }
                                
                                Console.WriteLine($"[WebSocket] Processed Alert - DroneId: '{alert.DroneId}', Location: {alert.AlertLocation}, Score: {alert.Score}");
                                LogAlertToFile(alert);
                                AlertReceived?.Invoke(this, new AlertReceivedEventArgs(alert));
                            }
                        }
                    }
                    else if (type == "alert" && msgObj.ContainsKey("data"))
                    {
                        // Handle the Python script format (direct from drone)
                        var dataJson = msgObj["data"]?.ToString();
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            Console.WriteLine($"[WebSocket] Alert data JSON: {dataJson}");
                            
                            var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                            if (dataObj != null)
                            {
                                Console.WriteLine($"[WebSocket] Data object keys: {string.Join(", ", dataObj.Keys)}");
                                
                                var alert = new AlertData
                                {
                                    Alert = dataObj.ContainsKey("alert") ? dataObj["alert"]?.ToString() : "Unknown Alert",
                                    DroneId = dataObj.ContainsKey("drone_id") ? dataObj["drone_id"]?.ToString() : "Unknown",
                                    Score = dataObj.ContainsKey("score") ? Convert.ToDouble(dataObj["score"]) : 0.0,
                                    RLResponsed = 0, // Default value
                                    ImageReceived = 0, // Default value
                                    Image = null,
                                    Timestamp = dataObj.ContainsKey("timestamp") ? dataObj["timestamp"]?.ToString() : DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")
                                };
                                
                                // Parse alert_location array
                                if (dataObj.ContainsKey("alert_location") && dataObj["alert_location"] != null)
                                {
                                    var locationStr = dataObj["alert_location"]?.ToString();
                                    Console.WriteLine($"[WebSocket] Location string: {locationStr}");
                                    
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(locationStr))
                                        {
                                            var coords = JsonConvert.DeserializeObject<double[]>(locationStr);
                                            if (coords != null && coords.Length >= 3)
                                            {
                                                alert.AlertLocation = new Tuple<double, double, double>(coords[0], coords[1], coords[2]);
                                                Console.WriteLine($"[WebSocket] Parsed location: [{coords[0]}, {coords[1]}, {coords[2]}]");
                                            }
                                        }
                                    }
                                    catch (Exception locEx)
                                    {
                                        Console.WriteLine($"[WebSocket] Location parsing error: {locEx.Message}");
                                    }
                                }
                                
                                Console.WriteLine($"[WebSocket] Processed Alert - DroneId: '{alert.DroneId}', Location: {alert.AlertLocation}, Score: {alert.Score}");
                                LogAlertToFile(alert);
                                AlertReceived?.Invoke(this, new AlertReceivedEventArgs(alert));
                            }
                        }
                    }
                    else if (type == "initial_alerts")
                    {
                        // Ignore initial alerts to ensure only real-time alerts are shown
                        Console.WriteLine($"[WebSocket] Received initial alerts - ignoring to maintain clean state");
                        if (msgObj.ContainsKey("alerts"))
                        {
                            var alertsJson = msgObj["alerts"]?.ToString();
                            if (!string.IsNullOrEmpty(alertsJson))
                            {
                                var alerts = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(alertsJson);
                                if (alerts != null)
                                {
                                    Console.WriteLine($"[WebSocket] Ignoring {alerts.Count} initial alerts to maintain clean state");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Log any other message types we receive
                        Console.WriteLine($"[WebSocket] Unhandled message type: {type}");
                        Console.WriteLine($"[WebSocket] Full message content: {message}");
                    }
                }
                else
                {
                    Console.WriteLine($"[WebSocket] Message doesn't contain 'type' field or is not valid JSON");
                    Console.WriteLine($"[WebSocket] Raw message: {message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebSocket message error: {ex.Message}");
                Console.WriteLine($"[WebSocket] Error processing message: {ex.Message}");
            }
        }

        private void LogAlertToFile(AlertData? alert)
        {
            try
            {
                if (alert == null) return;
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert_log.txt");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ALERT: {alert.Alert} | Drone: {alert.DroneId} | Location: {alert.AlertLocation} | Score: {alert.Score} | Timestamp: {alert.Timestamp}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging alert: {ex.Message}");
            }
        }

        // Comprehensive logging methods
        private void InitializeLogFile()
        {
            try
            {
                var logHeader = $"\n{'='*80}\n" +
                               $"WebSocket Communication Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                               $"Application: Drone Surveillance System\n" +
                               $"WebSocket URL: {_wsUrl}\n" +
                               $"{'='*80}\n\n";
                
                File.WriteAllText(_logFilePath, logHeader);
                LogToFile("SYSTEM", "Logger initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Failed to initialize log file: {ex.Message}");
            }
        }

        private void LogToFile(string type, string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{type}] {message}\n";
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Failed to log to file: {ex.Message}");
            }
        }

        private void StartHeartbeatTimer()
        {
            try
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = new Timer(SendHeartbeat, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                LogToFile("HEARTBEAT", "Heartbeat timer started");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Failed to start heartbeat timer: {ex.Message}");
            }
        }

        private void StopHeartbeatTimer()
        {
            try
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
                LogToFile("HEARTBEAT", "Heartbeat timer stopped");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Failed to stop heartbeat timer: {ex.Message}");
            }
        }

        private void SendHeartbeat(object? state)
        {
            try
            {
                if (_client != null && _isConnected && _client.IsRunning)
                {
                    var heartbeat = new { type = "ping", timestamp = DateTime.UtcNow.ToString("o") };
                    var json = JsonConvert.SerializeObject(heartbeat);
                    _client.SendInstant(json);
                    LogToFile("SENT", $"Heartbeat: {json}");
                    Console.WriteLine($"[WebSocket] 💓 Heartbeat sent");
                }
                else
                {
                    Console.WriteLine($"[WebSocket] 💓 Heartbeat skipped - client not connected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Failed to send heartbeat: {ex.Message}");
            }
        }

        public void StopWebSocket()
        {
            try
            {
                StopHeartbeatTimer();
                
                if (_client != null)
                {
                    _client.Stop(WebSocketCloseStatus.NormalClosure, "Application shutdown");
                    _client.Dispose();
                    _client = null;
                }
                
                _isConnected = false;
                LogToFile("DISCONNECT", "WebSocket stopped by application");
                Console.WriteLine($"[WebSocket] 🛑 WebSocket stopped by application");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Error stopping WebSocket: {ex.Message}");
            }
        }

        public bool IsConnected => _isConnected;

        public async Task StopWebSocketAsync()
        {
            await Task.Run(() => StopWebSocket());
        }

        public async Task ReconnectAsync()
        {
            try
            {
                StopWebSocket();
                await Task.Delay(1000); // Wait 1 second before reconnecting
                await StartWebSocketAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Reconnection failed: {ex.Message}");
                throw;
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                if (_client != null && _isConnected)
                {
                    _client.Send(message);
                    LogToFile("SENT", message);
                    Console.WriteLine($"[WebSocket] 📤 Message sent to server");
                }
                else
                {
                    Console.WriteLine($"[WebSocket] ❌ Cannot send message - WebSocket not connected");
                    throw new InvalidOperationException("WebSocket is not connected");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] ❌ Error sending message: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            StopWebSocket();
            _httpClient?.Dispose();
        }
    }

    // API Response wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public string? ErrorMessage { get; set; }
    }

    // API Result models
    public class GroupCreationResult
    {
        public int GroupId { get; set; }
        public string? Message { get; set; }
    }

    public class DroneRegistrationResult
    {
        public int DroneId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }

    public class DataUploadResult
    {
        public string? FileId { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }

    public class ControlCommandResult
    {
        public string? Command { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }
}