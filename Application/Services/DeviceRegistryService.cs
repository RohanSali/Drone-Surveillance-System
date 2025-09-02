using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DroneSurveillanceSystem.Services
{
    public enum DeviceType
    {
        Drone,
        CCTV
    }

    public class SurveillanceDevice
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public DeviceType Type { get; set; } = DeviceType.Drone;
        public string? Notes { get; set; }
    }

    public class DeviceRegistryService
    {
        private const string StorageFile = "devices.json";
        private readonly List<SurveillanceDevice> _devices;

        public DeviceRegistryService()
        {
            _devices = new List<SurveillanceDevice>();
            Load();
        }

        public List<SurveillanceDevice> GetDevices(DeviceType? type = null)
        {
            if (type.HasValue)
            {
                return _devices.Where(d => d.Type == type.Value).ToList();
            }
            return _devices.ToList();
        }

        public SurveillanceDevice? GetById(string id)
        {
            return _devices.FirstOrDefault(d => d.Id == id);
        }

        public SurveillanceDevice? GetByName(DeviceType type, string name)
        {
            return _devices.FirstOrDefault(d => d.Type == type && string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void RegisterOrUpdateDevice(SurveillanceDevice device)
        {
            var existing = _devices.FirstOrDefault(d => d.Id == device.Id);
            if (existing == null)
            {
                _devices.Add(device);
            }
            else
            {
                existing.Name = device.Name;
                existing.Type = device.Type;
                existing.Notes = device.Notes;
            }
            Save();
        }

        public bool RemoveDevice(string id)
        {
            var existing = _devices.FirstOrDefault(d => d.Id == id);
            if (existing != null)
            {
                _devices.Remove(existing);
                Save();
                return true;
            }
            return false;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(StorageFile))
                {
                    var json = File.ReadAllText(StorageFile);
                    var list = JsonSerializer.Deserialize<List<SurveillanceDevice>>(json);
                    if (list != null)
                    {
                        _devices.Clear();
                        _devices.AddRange(list);
                    }
                }
            }
            catch
            {
                // Ignore malformed file and start fresh
                _devices.Clear();
            }
        }

        private void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_devices, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(StorageFile, json);
            }
            catch
            {
                // Swallow errors to avoid crashing UI on save
            }
        }
    }
}


