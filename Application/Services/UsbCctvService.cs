using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services
{
    public class UsbCctv
    {
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Disconnected";
        public string UsbPort { get; set; } = "";
        public string DeviceId { get; set; } = "";
        public bool IsConnected { get; set; } = false;
        public string FirmwareVersion { get; set; } = "v1.0.0";
        public string Resolution { get; set; } = "1080p";
        public int FrameRate { get; set; } = 30;
        public string BluetoothMacAddress { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string SimType { get; set; } = "";
        public string Location { get; set; } = "";
    }

    public class UsbCctvService
    {
        private List<UsbCctv> _connectedCctvs = new List<UsbCctv>();
        private readonly object _lockObject = new object();

        public event EventHandler<List<UsbCctv>>? CctvListChanged;

        public List<UsbCctv> GetConnectedCctvs()
        {
            lock (_lockObject)
            {
                return _connectedCctvs.ToList();
            }
        }

        public async Task<List<UsbCctv>> DetectUsbCctvsAsync()
        {
            return await Task.Run(() =>
            {
                var detected = new List<UsbCctv>();
                try
                {
                    var sample = new[]
                    {
                        new UsbCctv { Name = "Temp CCTV A", UsbPort = "COM11", DeviceId = "USB_CCTV_001", FirmwareVersion = "v1.2.0", Resolution = "1080p", FrameRate = 30 },
                        new UsbCctv { Name = "Temp CCTV B", UsbPort = "COM12", DeviceId = "USB_CCTV_002", FirmwareVersion = "v1.1.5", Resolution = "2K", FrameRate = 25 },
                        new UsbCctv { Name = "Temp CCTV C", UsbPort = "COM13", DeviceId = "USB_CCTV_003", FirmwareVersion = "v1.0.9", Resolution = "720p", FrameRate = 30 }
                    };
                    detected.AddRange(sample);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error detecting USB CCTVs: {ex.Message}");
                }

                lock (_lockObject)
                {
                    _connectedCctvs = detected;
                }

                CctvListChanged?.Invoke(this, detected);
                return detected;
            });
        }

        public async Task<bool> ConnectAsync(string name)
        {
            return await Task.Run(() =>
            {
                var cam = _connectedCctvs.FirstOrDefault(c => c.Name == name);
                if (cam == null) return false;
                cam.Status = "Connected - Ready";
                cam.IsConnected = true;
                return true;
            });
        }

        public async Task<bool> FetchDetailsAsync(string name)
        {
            return await Task.Run(() =>
            {
                var cam = _connectedCctvs.FirstOrDefault(c => c.Name == name);
                if (cam == null || !cam.IsConnected) return false;
                cam.Status = $"Details fetched ({cam.Resolution} @ {cam.FrameRate} FPS)";
                return true;
            });
        }
    }
}


