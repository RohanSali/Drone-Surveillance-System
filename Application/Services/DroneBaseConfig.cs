using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace DroneSurveillanceSystem.Services
{
    /// <summary>
    /// Reads predefined home/base coordinates from appsettings.json (DroneBase section).
    /// </summary>
    public static class DroneBaseConfig
    {
        public static (double Latitude, double Longitude, double Altitude, double Yaw) Load()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(path))
                    return (0, 0, 0, 0);

                var root = JObject.Parse(File.ReadAllText(path));
                var node = root["DroneBase"] as JObject;
                if (node == null)
                    return (0, 0, 0, 0);

                double lat = node.Value<double?>("Latitude") ?? 0;
                double lon = node.Value<double?>("Longitude") ?? 0;
                double alt = node.Value<double?>("Altitude") ?? 0;
                double yaw = node.Value<double?>("Yaw") ?? 0;
                return (lat, lon, alt, yaw);
            }
            catch
            {
                return (0, 0, 0, 0);
            }
        }
    }
}
