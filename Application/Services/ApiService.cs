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
                    
                    // Special handling for alert_image_received messages (drone responses)
                    // Also handle direct alert_image messages from drone
                    if (type == "alert_image_received" || type == "alert_image")
                    {
                        Console.WriteLine($"[WebSocket] 🔍 ALERT IMAGE RECEIVED MESSAGE DETECTED!");
                        Console.WriteLine($"[WebSocket] Full alert_image_received message: {message}");
                        
                        // For direct alert_image messages, check both "alert_image" and "data" fields
                        string dataFieldName = msgObj.ContainsKey("alert_image") ? "alert_image" : "data";
                        
                        if (msgObj.ContainsKey(dataFieldName))
                        {
                            Console.WriteLine($"[WebSocket] ✅ {dataFieldName} field found in message");
                            var alertImageJson = msgObj[dataFieldName]?.ToString();
                            Console.WriteLine($"[WebSocket] {dataFieldName} JSON: {alertImageJson}");
                            
                            if (!string.IsNullOrEmpty(alertImageJson))
                            {
                                var alertImageObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(alertImageJson);
                                if (alertImageObj != null)
                                {
                                    Console.WriteLine($"[WebSocket] {dataFieldName} object keys: {string.Join(", ", alertImageObj.Keys)}");
                                    
                                    string actualImage = alertImageObj.ContainsKey("actual_image") ? alertImageObj["actual_image"]?.ToString() ?? "" : "";
                                    string matchedFrame = alertImageObj.ContainsKey("matched_frame") ? alertImageObj["matched_frame"]?.ToString() ?? "" : "";
                                    string location = alertImageObj.ContainsKey("location") ? alertImageObj["location"]?.ToString() ?? "" : "";
                                    string score = alertImageObj.ContainsKey("score") ? alertImageObj["score"]?.ToString() ?? "" : "";
                                    int found = alertImageObj.ContainsKey("found") ? Convert.ToInt32(alertImageObj["found"]) : 0;
                                    string name = alertImageObj.ContainsKey("name") ? alertImageObj["name"]?.ToString() ?? "" : "";
                                    
                                    Console.WriteLine($"[WebSocket] 📊 Parsed alert_image_received data:");
                                    Console.WriteLine($"[WebSocket]   - Name: '{name}'");
                                    Console.WriteLine($"[WebSocket]   - Found: {found}");
                                    Console.WriteLine($"[WebSocket]   - Location: {location}");
                                    Console.WriteLine($"[WebSocket]   - Score: {score}");
                                    Console.WriteLine($"[WebSocket]   - Actual image length: {actualImage?.Length ?? 0}");
                                    Console.WriteLine($"[WebSocket]   - Matched frame length: {matchedFrame?.Length ?? 0}");
                                    
                                    // Find the MonitoringAlertsPage and call HandleLostFindingResponse
                                    DroneSurveillanceSystem.Views.MonitoringAlertsPage? monitoringPage = null;
                                    
                                    // Check if MonitoringAlertsPage is open
                                    bool isPageOpen = DroneSurveillanceSystem.Views.MonitoringAlertsPage.IsMonitoringAlertsPageOpen();
                                    Console.WriteLine($"[WebSocket] 📋 MonitoringAlertsPage open status: {isPageOpen}");
                                    
                                    // First try: Look in all windows
                                    foreach (Window window in System.Windows.Application.Current.Windows)
                                    {
                                        Console.WriteLine($"[WebSocket] 🔍 Checking window: {window.GetType().Name}");
                                        if (window is DroneSurveillanceSystem.Views.MonitoringAlertsPage alertsPage)
                                        {
                                            monitoringPage = alertsPage;
                                            Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage in main windows list");
                                            break;
                                        }
                                    }
                                    
                                    // Second try: Look in owned windows of main window
                                    if (monitoringPage == null)
                                    {
                                        Console.WriteLine($"[WebSocket] 🔍 Checking owned windows of main window");
                                        var mainWindow = System.Windows.Application.Current.MainWindow;
                                        if (mainWindow != null)
                                        {
                                            Console.WriteLine($"[WebSocket] 📋 Main window type: {mainWindow.GetType().Name}");
                                            foreach (Window window in mainWindow.OwnedWindows)
                                            {
                                                Console.WriteLine($"[WebSocket] 🔍 Checking owned window: {window.GetType().Name}");
                                                if (window is DroneSurveillanceSystem.Views.MonitoringAlertsPage alertsPage)
                                                {
                                                    monitoringPage = alertsPage;
                                                    Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage in owned windows");
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[WebSocket] ❌ Main window is null");
                                        }
                                    }
                                    
                                    if (monitoringPage != null)
                                    {
                                        Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage, calling HandleLostFindingResponse");
                                        monitoringPage.HandleLostFindingResponse(
                                            actualImage ?? "", 
                                            matchedFrame ?? "", 
                                            location ?? "", 
                                            score ?? "", 
                                            found, 
                                            name ?? ""
                                        );
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[WebSocket] ❌ MonitoringAlertsPage not found! Total windows: {System.Windows.Application.Current.Windows.Count}");
                                        foreach (Window window in System.Windows.Application.Current.Windows)
                                        {
                                            Console.WriteLine($"[WebSocket] Window type: {window.GetType().Name}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[WebSocket] ❌ Failed to parse alert_image JSON");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[WebSocket] ❌ alert_image field is null or empty");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[WebSocket] ❌ alert_image field not found in message");
                        }
                    }
                    // Legacy support for image_received messages (keeping for backward compatibility)
                    else if (type == "image_received")
                    {
                        Console.WriteLine($"[WebSocket] 🔍 IMAGE RECEIVED MESSAGE DETECTED!");
                        Console.WriteLine($"[WebSocket] Full image_received message: {message}");
                        
                        if (msgObj.ContainsKey("data"))
                        {
                            Console.WriteLine($"[WebSocket] ✅ data field found in message");
                            var dataJson = msgObj["data"]?.ToString();
                            Console.WriteLine($"[WebSocket] data JSON: {dataJson}");
                            
                            if (!string.IsNullOrEmpty(dataJson))
                            {
                                var dataObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(dataJson);
                                if (dataObj != null)
                                {
                                    Console.WriteLine($"[WebSocket] data object keys: {string.Join(", ", dataObj.Keys)}");
                                    
                                    string actualImage = dataObj.ContainsKey("actual_image") ? dataObj["actual_image"]?.ToString() ?? "" : "";
                                    string matchedFrame = dataObj.ContainsKey("matched_frame") ? dataObj["matched_frame"]?.ToString() ?? "" : "";
                                    string location = dataObj.ContainsKey("location") ? dataObj["location"]?.ToString() ?? "" : "";
                                    string score = dataObj.ContainsKey("score") ? dataObj["score"]?.ToString() ?? "" : "";
                                    int found = dataObj.ContainsKey("found") ? Convert.ToInt32(dataObj["found"]) : 0;
                                    string name = dataObj.ContainsKey("name") ? dataObj["name"]?.ToString() ?? "" : "";
                                    
                                    Console.WriteLine($"[WebSocket] 📊 Parsed image_received data:");
                                    Console.WriteLine($"[WebSocket]   - Name: '{name}'");
                                    Console.WriteLine($"[WebSocket]   - Found: {found}");
                                    Console.WriteLine($"[WebSocket]   - Location: {location}");
                                    Console.WriteLine($"[WebSocket]   - Score: {score}");
                                    Console.WriteLine($"[WebSocket]   - Actual image length: {actualImage?.Length ?? 0}");
                                    Console.WriteLine($"[WebSocket]   - Matched frame length: {matchedFrame?.Length ?? 0}");
                                    
                                    // Find the MonitoringAlertsPage and call HandleLostFindingResponse
                                    DroneSurveillanceSystem.Views.MonitoringAlertsPage? monitoringPage = null;
                                    
                                    // Check if MonitoringAlertsPage is open
                                    bool isPageOpen = DroneSurveillanceSystem.Views.MonitoringAlertsPage.IsMonitoringAlertsPageOpen();
                                    Console.WriteLine($"[WebSocket] 📋 MonitoringAlertsPage open status: {isPageOpen}");
                                    
                                    // First try: Look in all windows
                                    foreach (Window window in System.Windows.Application.Current.Windows)
                                    {
                                        Console.WriteLine($"[WebSocket] 🔍 Checking window: {window.GetType().Name}");
                                        if (window is DroneSurveillanceSystem.Views.MonitoringAlertsPage alertsPage)
                                        {
                                            monitoringPage = alertsPage;
                                            Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage in main windows list");
                                            break;
                                        }
                                    }
                                    
                                    // Second try: Look in owned windows of main window
                                    if (monitoringPage == null)
                                    {
                                        Console.WriteLine($"[WebSocket] 🔍 Checking owned windows of main window");
                                        var mainWindow = System.Windows.Application.Current.MainWindow;
                                        if (mainWindow != null)
                                        {
                                            Console.WriteLine($"[WebSocket] 📋 Main window type: {mainWindow.GetType().Name}");
                                            foreach (Window window in mainWindow.OwnedWindows)
                                            {
                                                Console.WriteLine($"[WebSocket] 🔍 Checking owned window: {window.GetType().Name}");
                                                if (window is DroneSurveillanceSystem.Views.MonitoringAlertsPage alertsPage)
                                                {
                                                    monitoringPage = alertsPage;
                                                    Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage in owned windows");
                                                    break;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[WebSocket] ❌ Main window is null");
                                        }
                                    }
                                    
                                    if (monitoringPage != null)
                                    {
                                        Console.WriteLine($"[WebSocket] ✅ Found MonitoringAlertsPage, calling HandleLostFindingResponse");
                                        monitoringPage.HandleLostFindingResponse(
                                            actualImage ?? "", 
                                            matchedFrame ?? "", 
                                            location ?? "", 
                                            score ?? "", 
                                            found, 
                                            name ?? ""
                                        );
                                    }
                                    else
                                    {
                                        Console.WriteLine($"[WebSocket] ❌ MonitoringAlertsPage not found! Total windows: {System.Windows.Application.Current.Windows.Count}");
                                        foreach (Window window in System.Windows.Application.Current.Windows)
                                        {
                                            Console.WriteLine($"[WebSocket] Window type: {window.GetType().Name}");
                                        }
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"[WebSocket] ❌ Failed to parse data JSON");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"[WebSocket] ❌ data field is null or empty");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[WebSocket] ❌ data field not found in message");
                        }
                    }
                    else if (type == "new_alert" && msgObj.ContainsKey("alert"))
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
                Console.WriteLine($"[WebSocket] Logging error: {ex.Message}");
            }
        }

        // Heartbeat mechanism to keep connection alive
        private void StartHeartbeatTimer()
        {
            StopHeartbeatTimer(); // Stop any existing timer
            
            _heartbeatTimer = new Timer(_ =>
            {
                if (_client != null && _isConnected)
                {
                    try
                    {
                        var pingMessage = JsonConvert.SerializeObject(new { type = "ping", timestamp = DateTime.Now });
                        _client.Send(pingMessage);
                        LogToFile("SENT", $"Heartbeat ping: {pingMessage}");
                    }
                    catch (Exception ex)
                    {
                        LogToFile("ERROR", $"Heartbeat ping failed: {ex.Message}");
                        _isConnected = false;
                    }
                }
            }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)); // Send ping every 30 seconds
        }

        private void StopHeartbeatTimer()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        // Connection status check
        public bool IsConnected => _isConnected && _client?.IsRunning == true;

        // Manual reconnection method
        public async Task ReconnectAsync()
        {
            LogToFile("INFO", "Manual reconnection requested");
            await StopWebSocketAsync();
            await Task.Delay(2000); // Wait 2 seconds before reconnecting
            await StartWebSocketAsync();
        }

        // Stop WebSocket connection
        public async Task StopWebSocketAsync()
        {
            try
            {
                StopHeartbeatTimer();
                
                if (_client != null)
                {
                    LogToFile("DISCONNECT", "Stopping WebSocket connection");
                    await _client.Stop(WebSocketCloseStatus.NormalClosure, "Application shutdown");
                    _client.Dispose();
                    _client = null;
                }
                
                _isConnected = false;
                LogToFile("DISCONNECT", "WebSocket connection stopped");
            }
            catch (Exception ex)
            {
                LogToFile("ERROR", $"Error stopping WebSocket: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                StopHeartbeatTimer();
                _client?.Dispose();
                _httpClient?.Dispose();
                LogToFile("SYSTEM", "ApiService disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WebSocket] Dispose error: {ex.Message}");
            }
        }
    }

    // API Response Models
    public class ApiResponse<T>
    {
        public bool Success { get; set; } = false;
        public T? Data { get; set; } = default!;
        public string? ErrorMessage { get; set; } = string.Empty;
    }

    public class GroupCreationResult
    {
        [JsonProperty("group_id")]
        public int GroupId { get; set; }
    }

    public class DroneRegistrationResult
    {
        [JsonProperty("group_id")]
        public int GroupId { get; set; }
        public string Error { get; set; } = string.Empty;
    }

    public class DataUploadResult
    {
        public string Status { get; set; } = string.Empty;
    }

    public class ControlCommandResult
    {
        public string Status { get; set; } = string.Empty;
    }
}
