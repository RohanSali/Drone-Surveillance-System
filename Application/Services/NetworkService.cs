using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq; // Added for .FirstOrDefault() and .Sum()
using System.IO;
using System.Text.Json;
using DroneSurveillanceSystem.Services; // For DroneFlightStatus

namespace DroneSurveillanceSystem.Services
{
    public class Network : INotifyPropertyChanged
    {
        private string _name;
        private string _description;
        private string _status;
        private string _statusColor;
        private string _iconColor;
        private int _droneCount;
        private int _alertCount;
        private string _coverageRegion;
        private string _priorityLevel;
        private string _operationMode;
        private bool _autoActivate;
        private bool _alertNotifications;
        private List<DronePosition> _assignedDrones;
        private List<SurveillanceDevice> _assignedCctvs;
        private DateTime _createdDate;
        private DateTime _lastModified;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public string IconColor
        {
            get => _iconColor;
            set => SetProperty(ref _iconColor, value);
        }

        public int DroneCount
        {
            get => _droneCount;
            set => SetProperty(ref _droneCount, value);
        }

        public int AlertCount
        {
            get => _alertCount;
            set => SetProperty(ref _alertCount, value);
        }
        
        public string CoverageRegion
        {
            get => _coverageRegion;
            set => SetProperty(ref _coverageRegion, value);
        }
        
        public string PriorityLevel
        {
            get => _priorityLevel;
            set => SetProperty(ref _priorityLevel, value);
        }
        
        public string OperationMode
        {
            get => _operationMode;
            set => SetProperty(ref _operationMode, value);
        }
        
        public bool AutoActivate
        {
            get => _autoActivate;
            set => SetProperty(ref _autoActivate, value);
        }
        
        public bool AlertNotifications
        {
            get => _alertNotifications;
            set => SetProperty(ref _alertNotifications, value);
        }
        
        public List<DronePosition> Drones
        {
            get => _assignedDrones ?? new List<DronePosition>();
            set => SetProperty(ref _assignedDrones, value);
        }
        
        public List<SurveillanceDevice> Cctvs
        {
            get => _assignedCctvs ?? new List<SurveillanceDevice>();
            set => SetProperty(ref _assignedCctvs, value);
        }
        
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }
        
        public DateTime LastModified
        {
            get => _lastModified;
            set => SetProperty(ref _lastModified, value);
        }
        
        public Network()
        {
            _name = "";
            _description = "";
            _status = "Active";
            _statusColor = "#4CAF50";
            _iconColor = "#4CAF50";
            _assignedDrones = new List<DronePosition>();
            _assignedCctvs = new List<SurveillanceDevice>();
            _createdDate = DateTime.Now;
            _lastModified = DateTime.Now;
            _coverageRegion = "Urban Zone";
            _priorityLevel = "Medium Priority";
            _operationMode = "Patrol Mode";
            _alertNotifications = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class NetworkStatistics : INotifyPropertyChanged
    {
        private int _totalNetworks;
        private int _activeNetworks;
        private int _totalDrones;
        private int _activeAlerts;

        public int TotalNetworks
        {
            get => _totalNetworks;
            set => SetProperty(ref _totalNetworks, value);
        }

        public int ActiveNetworks
        {
            get => _activeNetworks;
            set => SetProperty(ref _activeNetworks, value);
        }

        public int TotalDrones
        {
            get => _totalDrones;
            set => SetProperty(ref _totalDrones, value);
        }

        public int ActiveAlerts
        {
            get => _activeAlerts;
            set => SetProperty(ref _activeAlerts, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public class NetworkService
    {
        private readonly ObservableCollection<Network> _networks;
        private readonly NetworkStatistics _statistics;
        private static string _currentUserKey = "guest";
        private static readonly object _lockObject = new object();

        private string StorageFile => Path.Combine("assets","networks",$"networks_{Sanitize(_currentUserKey)}.json");

        private static string Sanitize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "guest";
            var invalidChars = Path.GetInvalidFileNameChars();
            var chars = input.Select(c => invalidChars.Contains(c) || !(char.IsLetterOrDigit(c) || c == '@' || c == '.') ? '_' : c).ToArray();
            return new string(chars);
        }

        public static void SetCurrentUser(string? userEmailOrId)
        {
            lock (_lockObject)
            {
                _currentUserKey = string.IsNullOrWhiteSpace(userEmailOrId) ? "guest" : userEmailOrId.Trim();
            }
        }

        public ObservableCollection<Network> Networks => _networks;
        public NetworkStatistics Statistics => _statistics;

        public event EventHandler? StatisticsUpdated;

        public NetworkService()
        {
            _networks = new ObservableCollection<Network>();
            _statistics = new NetworkStatistics();
            // Ensure storage directory exists
            var storageDir = Path.GetDirectoryName(StorageFile);
            if (!string.IsNullOrEmpty(storageDir) && !Directory.Exists(storageDir))
            {
                Directory.CreateDirectory(storageDir);
            }
            LoadNetworks();
            UpdateStatistics();
        }

        private void LoadNetworks()
        {
            _networks.Clear();
            if (File.Exists(StorageFile))
            {
                var json = File.ReadAllText(StorageFile);
                var loaded = JsonSerializer.Deserialize<List<Network>>(json);
                if (loaded != null)
                {
                    foreach (var net in loaded)
                    {
                        _networks.Add(net);
                        net.PropertyChanged += OnNetworkPropertyChanged;
                    }
                }
            }
        }

        private void SaveNetworks()
        {
            var list = _networks.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StorageFile, json);
        }

        public void OnNetworkPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Network.Status) || 
                e.PropertyName == nameof(Network.DroneCount) || 
                e.PropertyName == nameof(Network.AlertCount))
            {
                UpdateStatistics();
            }
        }

        private void UpdateStatistics()
        {
            _statistics.TotalNetworks = _networks.Count;
            _statistics.ActiveNetworks = _networks.Count(n => n.Status == "Active");
            _statistics.TotalDrones = _networks.Sum(n => n.DroneCount);
            _statistics.ActiveAlerts = _networks.Sum(n => n.AlertCount);

            StatisticsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateNetworkStatus(string networkName, string newStatus)
        {
            var network = _networks.FirstOrDefault(n => n.Name == networkName);
            if (network != null)
            {
                network.Status = newStatus;
                // Update status color based on new status
                network.StatusColor = newStatus switch
                {
                    "Active" => "#4CAF50",
                    "Standby" => "#FF9800",
                    "Offline" => "#F44336",
                    "Testing" => "#9C27B0",
                    "Deployed" => "#00BCD4",
                    _ => "#cccccc"
                };
                SaveNetworks(); // Save after status change
            }
        }

        public void UpdateNetworkDrones(string networkName, int droneCount)
        {
            var network = _networks.FirstOrDefault(n => n.Name == networkName);
            if (network != null)
            {
                network.DroneCount = droneCount;
                SaveNetworks(); // Save after drone count change
            }
        }

        public void UpdateNetworkAlerts(string networkName, int alertCount)
        {
            var network = _networks.FirstOrDefault(n => n.Name == networkName);
            if (network != null)
            {
                network.AlertCount = alertCount;
                SaveNetworks(); // Save after alert count change
            }
        }

        // Call SaveNetworks after any add/edit/delete
        public void AddNetwork(Network network)
        {
            _networks.Add(network);
            network.PropertyChanged += OnNetworkPropertyChanged;
            SaveNetworks();
            UpdateStatistics();
        }

        public void RemoveNetwork(Network network)
        {
            _networks.Remove(network);
            SaveNetworks();
            UpdateStatistics();
        }

        public void UpdateNetwork(Network updatedNetwork)
        {
            var existing = _networks.FirstOrDefault(n => n.Name == updatedNetwork.Name);
            if (existing != null)
            {
                var idx = _networks.IndexOf(existing);
                _networks[idx] = updatedNetwork;
                SaveNetworks();
                UpdateStatistics();
            }
        }
    }
}
