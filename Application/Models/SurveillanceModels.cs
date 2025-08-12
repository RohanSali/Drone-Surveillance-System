using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DroneSurveillanceSystem.Models
{
    public class DetectionEvent : INotifyPropertyChanged
    {
        private DateTime _timestamp;
        private string _zone = string.Empty;
        private string _status = string.Empty;
        private string _droneId = string.Empty;
        private double _latitude;
        private double _longitude;
        private int _crowdCount;

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); }
        }

        public string Zone
        {
            get => _zone;
            set { _zone = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string DroneId
        {
            get => _droneId;
            set { _droneId = value; OnPropertyChanged(); }
        }

        public double Latitude
        {
            get => _latitude;
            set { _latitude = value; OnPropertyChanged(); }
        }

        public double Longitude
        {
            get => _longitude;
            set { _longitude = value; OnPropertyChanged(); }
        }

        public int CrowdCount
        {
            get => _crowdCount;
            set { _crowdCount = value; OnPropertyChanged(); }
        }

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        public string FormattedTime => Timestamp.ToString("HH:mm");
        public string GpsCoordinates => $"{Latitude:F6}, {Longitude:F6}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DroneStatus : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private bool _isActive;
        private string _currentZone = string.Empty;
        private double _batteryLevel;
        private double _altitude;
        private string _cameraAngle = string.Empty;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public string CurrentZone
        {
            get => _currentZone;
            set { _currentZone = value; OnPropertyChanged(); }
        }

        public double BatteryLevel
        {
            get => _batteryLevel;
            set { _batteryLevel = value; OnPropertyChanged(); }
        }

        public double Altitude
        {
            get => _altitude;
            set { _altitude = value; OnPropertyChanged(); }
        }

        public string CameraAngle
        {
            get => _cameraAngle;
            set { _cameraAngle = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SurveillanceSettings : INotifyPropertyChanged
    {
        private bool _aiDetectionEnabled;
        private bool _voiceAlertsEnabled;
        private int _detectionSensitivity;
        private string _selectedCamera = string.Empty;

        public bool AiDetectionEnabled
        {
            get => _aiDetectionEnabled;
            set { _aiDetectionEnabled = value; OnPropertyChanged(); }
        }

        public bool VoiceAlertsEnabled
        {
            get => _voiceAlertsEnabled;
            set { _voiceAlertsEnabled = value; OnPropertyChanged(); }
        }

        public int DetectionSensitivity
        {
            get => _detectionSensitivity;
            set { _detectionSensitivity = value; OnPropertyChanged(); }
        }

        public string SelectedCamera
        {
            get => _selectedCamera;
            set { _selectedCamera = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SurveillanceAlert : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _type = string.Empty;
        private string _message = string.Empty;
        private string _status = string.Empty;
        private DateTime _timestamp;
        private string _deviceId = string.Empty;
        private string _location = string.Empty;
        private int _severity;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(); }
        }

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public int Severity
        {
            get => _severity;
            set { _severity = value; OnPropertyChanged(); }
        }

        public string FormattedTimestamp => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SurveillanceDevice : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _deviceType = string.Empty;
        private string _status = string.Empty;
        private string _location = string.Empty;
        private DateTime _lastSeen;
        private string _firmwareVersion = string.Empty;
        private string _ipAddress = string.Empty;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string DeviceType
        {
            get => _deviceType;
            set { _deviceType = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string Location
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }

        public DateTime LastSeen
        {
            get => _lastSeen;
            set { _lastSeen = value; OnPropertyChanged(); }
        }

        public string FirmwareVersion
        {
            get => _firmwareVersion;
            set { _firmwareVersion = value; OnPropertyChanged(); }
        }

        public string IpAddress
        {
            get => _ipAddress;
            set { _ipAddress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SurveillanceNetwork : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _region = string.Empty;
        private string _operationMode = string.Empty;
        private bool _autoActivateOnDetection;
        private List<SurveillanceDevice> _drones = new List<SurveillanceDevice>();
        private List<SurveillanceDevice> _cctvs = new List<SurveillanceDevice>();
        private DateTime _createdAt;
        private DateTime _lastModified;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Region
        {
            get => _region;
            set { _region = value; OnPropertyChanged(); }
        }

        public string OperationMode
        {
            get => _operationMode;
            set { _operationMode = value; OnPropertyChanged(); }
        }

        public bool AutoActivateOnDetection
        {
            get => _autoActivateOnDetection;
            set { _autoActivateOnDetection = value; OnPropertyChanged(); }
        }

        public List<SurveillanceDevice> Drones
        {
            get => _drones;
            set { _drones = value; OnPropertyChanged(); }
        }

        public List<SurveillanceDevice> Cctvs
        {
            get => _cctvs;
            set { _cctvs = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public DateTime LastModified
        {
            get => _lastModified;
            set { _lastModified = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class UserProfile : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _email = string.Empty;
        private string _name = string.Empty;
        private DateTime _lastSignIn;
        private string _role = string.Empty;
        private bool _isActive;

        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public DateTime LastSignIn
        {
            get => _lastSignIn;
            set { _lastSignIn = value; OnPropertyChanged(); }
        }

        public string Role
        {
            get => _role;
            set { _role = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
