using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using DroneSurveillanceSystem.Views;

namespace DroneSurveillanceSystem.Services
{
    public class LostFindingManager
    {
        private static LostFindingManager? _instance;
        public static LostFindingManager Instance => _instance ??= new LostFindingManager();

        private readonly string _dataDirectory;
        private readonly string _pendingRequestsFile;
        private readonly ObservableCollection<PendingRequest> _pendingRequests;
        private string? _currentUserId;

        public ObservableCollection<PendingRequest> PendingRequests => _pendingRequests;

        private LostFindingManager()
        {
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DroneSurveillance",
                "LostFinding"
            );
            
            _pendingRequestsFile = Path.Combine(_dataDirectory, "pending_requests.json");
            _pendingRequests = new ObservableCollection<PendingRequest>();
            
            // Ensure directory exists
            Directory.CreateDirectory(_dataDirectory);
        }

        public void SetCurrentUser(string? userId)
        {
            _currentUserId = userId;
            LoadPendingRequests();
        }

        public void AddPendingRequest(PendingRequest request)
        {
            if (string.IsNullOrEmpty(_currentUserId))
            {
                MessageBox.Show("Please sign in to save pending requests.", "Authentication Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            request.UserId = _currentUserId;
            _pendingRequests.Add(request);
            SavePendingRequests();
        }

        public void RemovePendingRequest(PendingRequest request)
        {
            _pendingRequests.Remove(request);
            SavePendingRequests();
        }

        public void UpdatePendingRequest(PendingRequest request)
        {
            var existingRequest = _pendingRequests.FirstOrDefault(r => r.Name == request.Name);
            if (existingRequest != null)
            {
                var index = _pendingRequests.IndexOf(existingRequest);
                _pendingRequests[index] = request;
                SavePendingRequests();
            }
        }

        private void LoadPendingRequests()
        {
            _pendingRequests.Clear();
            
            if (string.IsNullOrEmpty(_currentUserId) || !File.Exists(_pendingRequestsFile))
                return;

            try
            {
                var json = File.ReadAllText(_pendingRequestsFile);
                var allRequests = JsonSerializer.Deserialize<List<PendingRequestData>>(json) ?? new List<PendingRequestData>();
                
                // Filter requests for current user
                var userRequests = allRequests.Where(r => r.UserId == _currentUserId).ToList();
                
                foreach (var requestData in userRequests)
                {
                    var request = new PendingRequest
                    {
                        Name = requestData.Name,
                        Timestamp = requestData.Timestamp,
                        Status = requestData.Status,
                        StatusColor = requestData.StatusColor,
                        ImageBase64 = requestData.ImageBase64,
                        MatchedImageBase64 = requestData.MatchedImageBase64,
                        MatchScore = requestData.MatchScore,
                        IsSelected = false,
                        UserId = requestData.UserId
                    };
                    _pendingRequests.Add(request);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading pending requests: {ex.Message}");
            }
        }

        private void SavePendingRequests()
        {
            if (string.IsNullOrEmpty(_currentUserId))
                return;

            try
            {
                // Load all existing requests
                var allRequests = new List<PendingRequestData>();
                if (File.Exists(_pendingRequestsFile))
                {
                    var json = File.ReadAllText(_pendingRequestsFile);
                    allRequests = JsonSerializer.Deserialize<List<PendingRequestData>>(json) ?? new List<PendingRequestData>();
                }

                // Remove current user's requests
                allRequests.RemoveAll(r => r.UserId == _currentUserId);

                // Add current user's requests
                foreach (var request in _pendingRequests)
                {
                    allRequests.Add(new PendingRequestData
                    {
                        Name = request.Name,
                        Timestamp = request.Timestamp,
                        Status = request.Status,
                        StatusColor = request.StatusColor,
                        ImageBase64 = request.ImageBase64,
                        MatchedImageBase64 = request.MatchedImageBase64,
                        MatchScore = request.MatchScore,
                        UserId = request.UserId
                    });
                }

                // Save all requests
                var jsonString = JsonSerializer.Serialize(allRequests, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_pendingRequestsFile, jsonString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving pending requests: {ex.Message}");
            }
        }

        public void ClearAllPendingRequests()
        {
            _pendingRequests.Clear();
            SavePendingRequests();
        }

        public static void ResetInstance()
        {
            _instance = null;
        }
    }

    // Data transfer object for serialization
    public class PendingRequestData
    {
        public string Name { get; set; } = string.Empty;
        public string Timestamp { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#FF9800";
        public string ImageBase64 { get; set; } = string.Empty;
        public string MatchedImageBase64 { get; set; } = string.Empty;
        public double MatchScore { get; set; } = 0;
        public string UserId { get; set; } = string.Empty;
    }
}
