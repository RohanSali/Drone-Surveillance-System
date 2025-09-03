using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace DroneSurveillanceSystem.Services
{
    /// <summary>
    /// Singleton manager for Lost Finding data persistence across application sessions
    /// </summary>
    public class LostFindingManager : INotifyPropertyChanged
    {
        private static LostFindingManager? _instance;
        private static readonly object _lock = new object();
        
        private readonly string _dataFilePath;
        private readonly string _statusLogPath;
        private List<LostFindingRequest> _pendingRequests = new List<LostFindingRequest>();
        private List<LostFindingData> _lostFindingData = new List<LostFindingData>();

        // Use the same logger file as ApiService for raw comms
        private static readonly string LoggerPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logger.txt");

        public event PropertyChangedEventHandler? PropertyChanged;

        private LostFindingManager()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillanceSystem");
            Directory.CreateDirectory(appDataPath);
            _dataFilePath = Path.Combine(appDataPath, "lost_finding_data.json");
            _statusLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lost_finding_status.log");
            LoadData();
            InitializeStatusLog();
        }

        public static LostFindingManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new LostFindingManager();
                    }
                }
                return _instance;
            }
        }

        public List<string> PendingRequests => _pendingRequests.Select(r => r.Name).ToList();
        public List<LostFindingData> LostFindingData => _lostFindingData;
        public int PendingCount => _pendingRequests.Count(r => !r.IsCompleted);

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Public helper to log WebSocket communication to logger.txt (same format as ApiService)
        public static void LogWebSocket(string type, string raw)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] [{type}] {raw}\n";
                File.AppendAllText(LoggerPath, logEntry);
            }
            catch { }
        }

        // Status log (pending/fulfilled tracking)
        private void InitializeStatusLog()
        {
            try
            {
                var header = $"\n{'='*80}\n" +
                             $"Lost Finding Status Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                             $"Tracks pending and fulfilled Lost Finding items\n" +
                             $"{'='*80}\n\n";
                File.WriteAllText(_statusLogPath, header);
                LogStatus("SYSTEM", "Status logger initialized");
            }
            catch { }
        }

        private void LogStatus(string type, string message)
        {
            try
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                File.AppendAllText(_statusLogPath, $"[{ts}] [{type}] {message}\n");
            }
            catch { }
        }

        public void AddPendingRequest(string name, string actualImageBase64)
        {
            var request = new LostFindingRequest
            {
                Name = name,
                RequestId = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                Timestamp = DateTime.Now,
                IsCompleted = false
            };

            var lostFindingItem = new LostFindingData
            {
                RequestId = request.RequestId,
                ActualImageBase64 = actualImageBase64,
                MatchedImageBase64 = "",
                Location = "",
                Score = "",
                Timestamp = DateTime.Now,
                Name = name
            };

            _pendingRequests.Add(request);
            _lostFindingData.Add(lostFindingItem);
            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            LogStatus("PENDING", $"Added request: name='{name}', requestId='{request.RequestId}'");
        }

        public void AddPendingRequest(string name, LostFindingData lostFindingItem)
        {
            var request = new LostFindingRequest
            {
                Name = name,
                RequestId = lostFindingItem.RequestId,
                Timestamp = lostFindingItem.Timestamp,
                IsCompleted = false
            };

            _pendingRequests.Add(request);
            _lostFindingData.Add(lostFindingItem);
            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            LogStatus("PENDING", $"Added request: name='{name}', requestId='{lostFindingItem.RequestId}'");
        }

        // Extended to optionally update ActualImageBase64 from response
        public void HandleResponse(string name, string matchedImageBase64, string location, string score, int found, string actualImageBase64 = "")
        {
            if (!string.IsNullOrEmpty(name))
            {
                var matchingData = _lostFindingData.FirstOrDefault(data => data.Name == name);
                var matchingRequest = _pendingRequests.FirstOrDefault(r => r.Name == name && !r.IsCompleted);
                
                if (matchingData != null)
                {
                    if (!string.IsNullOrEmpty(actualImageBase64))
                    {
                        matchingData.ActualImageBase64 = actualImageBase64;
                    }
                    matchingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                    matchingData.Location = location;
                    matchingData.Score = score;
                    
                    if (matchingRequest != null)
                    {
                        matchingRequest.IsCompleted = true;
                    }
                    
                    SaveData();
                    
                    OnPropertyChanged(nameof(PendingCount));
                    OnPropertyChanged(nameof(PendingRequests));
                    OnPropertyChanged(nameof(LostFindingData));

                    LogStatus(found == 1 ? "FULFILLED" : "UPDATED", $"name='{name}', found={found}, location='{location}', score='{score}'");
                }
                else
                {
                    var oldestPending = _pendingRequests.FirstOrDefault(r => !r.IsCompleted);
                    if (oldestPending != null)
                    {
                        oldestPending.IsCompleted = true;
                        var correspondingData = _lostFindingData.FirstOrDefault(d => d.Name == oldestPending.Name);
                        if (correspondingData != null)
                        {
                            if (!string.IsNullOrEmpty(actualImageBase64))
                            {
                                correspondingData.ActualImageBase64 = actualImageBase64;
                            }
                            correspondingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                            correspondingData.Location = location;
                            correspondingData.Score = score;
                        }
                        
                        SaveData();
                        OnPropertyChanged(nameof(PendingCount));
                        OnPropertyChanged(nameof(PendingRequests));
                        OnPropertyChanged(nameof(LostFindingData));

                        LogStatus(found == 1 ? "FULFILLED" : "UPDATED", $"fallback matched to name='{oldestPending.Name}', found={found}");
                    }
                }
            }
            else
            {
                var oldestPending = _pendingRequests.FirstOrDefault(r => !r.IsCompleted);
                if (oldestPending != null)
                {
                    oldestPending.IsCompleted = true;
                    var correspondingData = _lostFindingData.FirstOrDefault(d => d.Name == oldestPending.Name);
                    if (correspondingData != null)
                    {
                        if (!string.IsNullOrEmpty(actualImageBase64))
                        {
                            correspondingData.ActualImageBase64 = actualImageBase64;
                        }
                        correspondingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                        correspondingData.Location = location;
                        correspondingData.Score = score;
                    }
                    
                    SaveData();
                    OnPropertyChanged(nameof(PendingCount));
                    OnPropertyChanged(nameof(PendingRequests));
                    OnPropertyChanged(nameof(LostFindingData));

                    LogStatus(found == 1 ? "FULFILLED" : "UPDATED", $"fallback (no name) matched to name='{oldestPending.Name}', found={found}");
                }
            }
        }
        
        public void CompletePendingRequest(string name, string matchedImageBase64, string location, string score, int found)
        {
            var request = _pendingRequests.FirstOrDefault(r => r.Name == name && !r.IsCompleted);
            var data = _lostFindingData.FirstOrDefault(d => d.Name == name);

            if (request != null && data != null)
            {
                request.IsCompleted = true;
                data.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                data.Location = location;
                data.Score = score;
                
                SaveData();
                
                OnPropertyChanged(nameof(PendingCount));
                OnPropertyChanged(nameof(PendingRequests));
                OnPropertyChanged(nameof(LostFindingData));

                LogStatus("FULFILLED", $"name='{name}', found={found}, score='{score}', location='{location}'");
            }
        }

        public void RemoveLostFindingData(string name)
        {
            var request = _pendingRequests.FirstOrDefault(r => r.Name == name);
            var data = _lostFindingData.FirstOrDefault(d => d.Name == name);

            if (request != null) _pendingRequests.Remove(request);
            if (data != null) _lostFindingData.Remove(data);

            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            LogStatus("REMOVED", $"name='{name}' removed from tracking");
        }

        public void ClearAllData()
        {
            _pendingRequests.Clear();
            _lostFindingData.Clear();
            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            LogStatus("CLEARED", "All lost finding data cleared");
        }

        private void SaveData()
        {
            try
            {
                var data = new
                {
                    PendingRequests = _pendingRequests,
                    LostFindingData = _lostFindingData,
                    LastUpdated = DateTime.UtcNow
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_dataFilePath, json);
            }
            catch { }
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_dataFilePath))
                {
                    var json = File.ReadAllText(_dataFilePath);
                    var data = JsonConvert.DeserializeObject<dynamic>(json);

                    if (data?.PendingRequests != null)
                    {
                        _pendingRequests = JsonConvert.DeserializeObject<List<LostFindingRequest>>(data.PendingRequests.ToString()) ?? new List<LostFindingRequest>();
                    }

                    if (data?.LostFindingData != null)
                    {
                        _lostFindingData = JsonConvert.DeserializeObject<List<LostFindingData>>(data.LostFindingData.ToString()) ?? new List<LostFindingData>();
                    }
                }
            }
            catch
            {
                _pendingRequests = new List<LostFindingRequest>();
                _lostFindingData = new List<LostFindingData>();
            }
        }
    }

    public class LostFindingRequest
    {
        public string RequestId { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class LostFindingData
    {
        public string RequestId { get; set; } = "";
        public string ActualImageBase64 { get; set; } = "";
        public string MatchedImageBase64 { get; set; } = "";
        public string Location { get; set; } = "";
        public string Score { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Name { get; set; } = "";
    }
}
