using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Linq; // Added for .FirstOrDefault() and .Sum()
using DroneSurveillanceSystem.Services; // For DroneFlightStatus
using DroneSurveillanceSystem.Services.Firebase;
using System.Net.Http;
using System.Threading;

namespace DroneSurveillanceSystem.Services
{
    public class Network : INotifyPropertyChanged
    {
        private string _networkId;
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

        public string NetworkId
        {
            get => _networkId;
            set => SetProperty(ref _networkId, value);
        }

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
            _networkId = Guid.NewGuid().ToString("N");
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
        private readonly HttpClient _httpClient;
        private readonly FirebaseAuthConfig? _firebaseConfig;

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
            _httpClient = new HttpClient();
            try
            {
                _firebaseConfig = FirebaseAuthConfig.Load();
            }
            catch
            {
                _firebaseConfig = null;
            }

            LoadNetworks();
            UpdateStatistics();
        }

        private void LoadNetworks()
        {
            _networks.Clear();
            var map = ReadNetworksFromFirebase();
            if (map != null)
            {
                foreach (var kvp in map)
                {
                    var net = FromFirebaseRecord(kvp.Key, kvp.Value);
                    _networks.Add(net);
                    net.PropertyChanged += OnNetworkPropertyChanged;
                }
            }
        }

        private string ResolveUserId()
        {
            var user = FirebaseSession.Current;
            if (!string.IsNullOrWhiteSpace(user?.AppClientId))
                return user.AppClientId;

            lock (_lockObject)
            {
                return string.IsNullOrWhiteSpace(_currentUserKey) ? "guest" : _currentUserKey;
            }
        }

        private bool TryCreateRtdbClient(out FirebaseRtdbRestClient? client, out string? token)
        {
            client = null;
            token = FirebaseSession.Current?.FirebaseIdToken;
            if (_firebaseConfig == null || string.IsNullOrWhiteSpace(token))
                return false;

            client = new FirebaseRtdbRestClient(_httpClient, _firebaseConfig);
            return true;
        }

        private Dictionary<string, FirebaseNetworkRecord>? ReadNetworksFromFirebase()
        {
            try
            {
                if (!TryCreateRtdbClient(out var rtdb, out var token) || rtdb == null || string.IsNullOrWhiteSpace(token))
                    return null;

                var userId = Uri.EscapeDataString(ResolveUserId());
                return rtdb.GetAsync<Dictionary<string, FirebaseNetworkRecord>>(
                    $"user_client_mapping/{userId}/networks",
                    token,
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private void SaveNetwork(Network network)
        {
            try
            {
                if (!TryCreateRtdbClient(out var rtdb, out var token) || rtdb == null || string.IsNullOrWhiteSpace(token))
                    return;

                if (string.IsNullOrWhiteSpace(network.NetworkId))
                {
                    network.NetworkId = Guid.NewGuid().ToString("N");
                }

                network.DroneCount = network.Drones?.Count ?? 0;
                network.LastModified = DateTime.Now;
                var userId = Uri.EscapeDataString(ResolveUserId());
                var networkId = Uri.EscapeDataString(network.NetworkId);
                var payload = ToFirebaseRecord(network);

                rtdb.PutAsync($"user_client_mapping/{userId}/networks/{networkId}", payload, token, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // keep UI usable even when Firebase write fails
            }
        }

        private void DeleteNetworkFromFirebase(Network network)
        {
            try
            {
                if (!TryCreateRtdbClient(out var rtdb, out var token) || rtdb == null || string.IsNullOrWhiteSpace(token))
                    return;
                if (string.IsNullOrWhiteSpace(network.NetworkId))
                    return;

                var userId = Uri.EscapeDataString(ResolveUserId());
                var networkId = Uri.EscapeDataString(network.NetworkId);
                rtdb.PutAsync<object?>($"user_client_mapping/{userId}/networks/{networkId}", null, token, CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // keep UI usable even when Firebase delete fails
            }
        }

        private static FirebaseNetworkRecord ToFirebaseRecord(Network network)
        {
            return new FirebaseNetworkRecord
            {
                Name = network.Name,
                Description = network.Description,
                Status = network.Status,
                StatusColor = network.StatusColor,
                IconColor = network.IconColor,
                DroneCount = network.DroneCount,
                AlertCount = network.AlertCount,
                CoverageRegion = network.CoverageRegion,
                PriorityLevel = network.PriorityLevel,
                OperationMode = network.OperationMode,
                AutoActivate = network.AutoActivate,
                AlertNotifications = network.AlertNotifications,
                Drones = (network.Drones ?? new List<DronePosition>())
                    .Select(d => d.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                Cctvs = (network.Cctvs ?? new List<SurveillanceDevice>())
                    .Select(c => c.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                CreatedDate = network.CreatedDate,
                LastModified = network.LastModified
            };
        }

        private static Network FromFirebaseRecord(string networkId, FirebaseNetworkRecord? record)
        {
            record ??= new FirebaseNetworkRecord();
            var droneLookup = DeviceDataManager.GetAllDrones()
                .Where(d => !string.IsNullOrWhiteSpace(d.DeviceId))
                .ToDictionary(d => d.DeviceId, d => d, StringComparer.OrdinalIgnoreCase);
            var cctvLookup = DeviceDataManager.GetAllCctvs()
                .Where(c => !string.IsNullOrWhiteSpace(c.DeviceId))
                .ToDictionary(c => c.DeviceId, c => c, StringComparer.OrdinalIgnoreCase);

            var drones = (record.Drones ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new DronePosition
                {
                    Id = id,
                    Name = droneLookup.TryGetValue(id, out var d) ? d.Name : id
                })
                .ToList();

            var cctvs = (record.Cctvs ?? new List<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => new SurveillanceDevice
                {
                    Id = id,
                    Name = cctvLookup.TryGetValue(id, out var c) ? c.Name : id,
                    Type = DeviceType.CCTV
                })
                .ToList();

            return new Network
            {
                NetworkId = networkId,
                Name = record.Name ?? string.Empty,
                Description = record.Description ?? string.Empty,
                Status = record.Status ?? "Active",
                StatusColor = record.StatusColor ?? "#4ACF50",
                IconColor = record.IconColor ?? "#4ACF50",
                DroneCount = drones.Count,
                AlertCount = record.AlertCount,
                CoverageRegion = record.CoverageRegion ?? "Urban Zone",
                PriorityLevel = record.PriorityLevel ?? "Medium Priority",
                OperationMode = record.OperationMode ?? "Surveillance Mode",
                AutoActivate = record.AutoActivate,
                AlertNotifications = record.AlertNotifications,
                Drones = drones,
                Cctvs = cctvs,
                CreatedDate = record.CreatedDate == default ? DateTime.Now : record.CreatedDate,
                LastModified = record.LastModified == default ? DateTime.Now : record.LastModified
            };
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
                SaveNetwork(network);
            }
        }

        public void UpdateNetworkDrones(string networkName, int droneCount)
        {
            var network = _networks.FirstOrDefault(n => n.Name == networkName);
            if (network != null)
            {
                network.DroneCount = droneCount;
                SaveNetwork(network);
            }
        }

        public void UpdateNetworkAlerts(string networkName, int alertCount)
        {
            var network = _networks.FirstOrDefault(n => n.Name == networkName);
            if (network != null)
            {
                network.AlertCount = alertCount;
                SaveNetwork(network);
            }
        }

        public void AddNetwork(Network network)
        {
            if (string.IsNullOrWhiteSpace(network.NetworkId))
                network.NetworkId = Guid.NewGuid().ToString("N");
            if (network.CreatedDate == default)
                network.CreatedDate = DateTime.Now;
            network.LastModified = DateTime.Now;
            network.DroneCount = network.Drones?.Count ?? 0;

            _networks.Add(network);
            network.PropertyChanged += OnNetworkPropertyChanged;
            SaveNetwork(network);
            UpdateStatistics();
        }

        public void RemoveNetwork(Network network)
        {
            network.PropertyChanged -= OnNetworkPropertyChanged;
            _networks.Remove(network);
            DeleteNetworkFromFirebase(network);
            UpdateStatistics();
        }

        public void UpdateNetwork(Network updatedNetwork)
        {
            var existing = _networks.FirstOrDefault(n =>
                (!string.IsNullOrWhiteSpace(updatedNetwork.NetworkId) && n.NetworkId == updatedNetwork.NetworkId) ||
                n.Name == updatedNetwork.Name);
            if (existing != null)
            {
                var idx = _networks.IndexOf(existing);
                if (string.IsNullOrWhiteSpace(updatedNetwork.NetworkId))
                    updatedNetwork.NetworkId = existing.NetworkId;
                if (updatedNetwork.CreatedDate == default)
                    updatedNetwork.CreatedDate = existing.CreatedDate;
                updatedNetwork.LastModified = DateTime.Now;
                updatedNetwork.DroneCount = updatedNetwork.Drones?.Count ?? 0;
                _networks[idx] = updatedNetwork;
                updatedNetwork.PropertyChanged += OnNetworkPropertyChanged;
                SaveNetwork(updatedNetwork);
                UpdateStatistics();
            }
        }
    }

    internal class FirebaseNetworkRecord
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? StatusColor { get; set; }
        public string? IconColor { get; set; }
        public int DroneCount { get; set; }
        public int AlertCount { get; set; }
        public string? CoverageRegion { get; set; }
        public string? PriorityLevel { get; set; }
        public string? OperationMode { get; set; }
        public bool AutoActivate { get; set; }
        public bool AlertNotifications { get; set; }
        public List<string>? Drones { get; set; }
        public List<string>? Cctvs { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
    }
}
