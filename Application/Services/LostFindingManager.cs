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
        private List<LostFindingRequest> _pendingRequests = new List<LostFindingRequest>();
        private List<LostFindingData> _lostFindingData = new List<LostFindingData>();

        public event PropertyChangedEventHandler? PropertyChanged;

        private LostFindingManager()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroneSurveillanceSystem");
            Directory.CreateDirectory(appDataPath);
            _dataFilePath = Path.Combine(appDataPath, "lost_finding_data.json");
            LoadData();
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
                Location = "0, 0, 0",
                Score = "Pending...",
                Timestamp = DateTime.Now,
                Name = name
            };

            _pendingRequests.Add(request);
            _lostFindingData.Add(lostFindingItem);
            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            Console.WriteLine($"[LostFindingManager] Added pending request: {name}, Total pending: {PendingCount}");
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

            Console.WriteLine($"[LostFindingManager] Added pending request: {name}, Total pending: {PendingCount}");
        }

        public void HandleResponse(string name, string matchedImageBase64, string location, string score, int found)
        {
            Console.WriteLine($"[LostFindingManager] 🔍 Handling response for: '{name}'");
            Console.WriteLine($"[LostFindingManager]   - Found: {found}");
            Console.WriteLine($"[LostFindingManager]   - Matched image length: {matchedImageBase64?.Length ?? 0}");
            Console.WriteLine($"[LostFindingManager]   - Total data items: {_lostFindingData.Count}");
            
            // If we have a name, try to find the matching request
            if (!string.IsNullOrEmpty(name))
            {
                var matchingData = _lostFindingData.FirstOrDefault(data => data.Name == name);
                var matchingRequest = _pendingRequests.FirstOrDefault(r => r.Name == name && !r.IsCompleted);
                
                if (matchingData != null)
                {
                    Console.WriteLine($"[LostFindingManager] ✅ Found matching request for name: {name}");
                    Console.WriteLine($"[LostFindingManager]   - Before update - MatchedImageBase64 length: {matchingData.MatchedImageBase64?.Length ?? 0}");
                    
                    // Update the matching data
                    matchingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                    matchingData.Location = location;
                    matchingData.Score = score;
                    
                    // Mark request as completed
                    if (matchingRequest != null)
                    {
                        matchingRequest.IsCompleted = true;
                        Console.WriteLine($"[LostFindingManager] ✅ Marked request as completed");
                    }
                    
                    Console.WriteLine($"[LostFindingManager]   - After update - MatchedImageBase64 length: {matchingData.MatchedImageBase64?.Length ?? 0}");
                    
                    // Save the updated data
                    SaveData();
                    
                    OnPropertyChanged(nameof(PendingCount));
                    OnPropertyChanged(nameof(PendingRequests));
                    OnPropertyChanged(nameof(LostFindingData));
                }
                else
                {
                    Console.WriteLine($"[LostFindingManager] ❌ No matching request found for name: {name}");
                    Console.WriteLine($"[LostFindingManager] Available names: {string.Join(", ", _lostFindingData.Select(d => $"'{d.Name}'"))}");
                    
                    // Fall back to the old behavior
                    var oldestPending = _pendingRequests.FirstOrDefault(r => !r.IsCompleted);
                    if (oldestPending != null)
                    {
                        oldestPending.IsCompleted = true;
                        
                        // Update the corresponding Lost Finding data
                        var correspondingData = _lostFindingData.FirstOrDefault(d => d.Name == oldestPending.Name);
                        if (correspondingData != null)
                        {
                            Console.WriteLine($"[LostFindingManager] 📝 Updating data for fallback request: {oldestPending.Name}");
                            // Only set matched image if found equals 1
                            correspondingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                            correspondingData.Location = location;
                            correspondingData.Score = score;
                        }
                        
                        SaveData();
                        OnPropertyChanged(nameof(PendingCount));
                        OnPropertyChanged(nameof(PendingRequests));
                        OnPropertyChanged(nameof(LostFindingData));
                    }
                }
            }
            else
            {
                Console.WriteLine($"[LostFindingManager] ⚠️ No name provided, using fallback behavior");
                // Fall back to the old behavior when no name is provided
                var oldestPending = _pendingRequests.FirstOrDefault(r => !r.IsCompleted);
                if (oldestPending != null)
                {
                    oldestPending.IsCompleted = true;
                    
                    // Update the corresponding Lost Finding data
                    var correspondingData = _lostFindingData.FirstOrDefault(d => d.Name == oldestPending.Name);
                    if (correspondingData != null)
                    {
                        Console.WriteLine($"[LostFindingManager] 📝 Updating data for oldest pending request: {oldestPending.Name}");
                        // Only set matched image if found equals 1
                        correspondingData.MatchedImageBase64 = found == 1 ? matchedImageBase64 : "";
                        correspondingData.Location = location;
                        correspondingData.Score = score;
                    }
                    
                    SaveData();
                    OnPropertyChanged(nameof(PendingCount));
                    OnPropertyChanged(nameof(PendingRequests));
                    OnPropertyChanged(nameof(LostFindingData));
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

                Console.WriteLine($"[LostFindingManager] Completed request: {name}, Remaining pending: {PendingCount}");
            }
            else
            {
                Console.WriteLine($"[LostFindingManager] Could not find pending request: {name}");
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

            Console.WriteLine($"[LostFindingManager] Removed data: {name}, Remaining pending: {PendingCount}");
        }

        public void ClearAllData()
        {
            _pendingRequests.Clear();
            _lostFindingData.Clear();
            SaveData();
            
            OnPropertyChanged(nameof(PendingCount));
            OnPropertyChanged(nameof(PendingRequests));
            OnPropertyChanged(nameof(LostFindingData));

            Console.WriteLine($"[LostFindingManager] Cleared all data");
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
            catch (Exception ex)
            {
                Console.WriteLine($"[LostFindingManager] Error saving data: {ex.Message}");
            }
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

                    Console.WriteLine($"[LostFindingManager] Loaded {_pendingRequests.Count} requests, {PendingCount} pending");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LostFindingManager] Error loading data: {ex.Message}");
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
