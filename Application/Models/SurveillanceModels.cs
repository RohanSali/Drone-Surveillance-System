using System;
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
}
