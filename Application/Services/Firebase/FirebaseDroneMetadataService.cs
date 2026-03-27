using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneSurveillanceSystem.Services;
using System.Text.Json;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseDroneMetadataService
    {
        private readonly FirebaseRtdbRestClient _rtdb;

        public FirebaseDroneMetadataService(FirebaseRtdbRestClient rtdb)
        {
            _rtdb = rtdb ?? throw new ArgumentNullException(nameof(rtdb));
        }

        public async Task UpsertDroneMetadataAsync(
            UsbDrone drone,
            IEnumerable<string>? networkAssignments,
            string firebaseIdToken,
            CancellationToken ct)
        {
            if (drone == null) throw new ArgumentNullException(nameof(drone));
            if (string.IsNullOrWhiteSpace(drone.DeviceId))
                throw new ArgumentException("Drone DeviceId is required.", nameof(drone));
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
                throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));
            // networkAssignments are intentionally NOT persisted under /drones anymore.

            var id = drone.DeviceId.Trim();
            var escapedId = Uri.EscapeDataString(id);

            // Granular writes so we do not overwrite fields like device_accessing.
            await _rtdb.PutAsync($"drones/{escapedId}/deviceId", drone.DeviceId?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/name", drone.Name?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/type", drone.DroneType?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/firmwareVersion", drone.FirmwareVersion?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/usbPort", drone.UsbPort?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/bluetoothMacAddress", drone.BluetoothMacAddress?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/ipAddress", drone.IpAddress?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"drones/{escapedId}/simType", drone.SimType?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);

            // Best-effort cleanup of older schema.
            try
            {
                await _rtdb.PutAsync<object?>($"drones/{escapedId}/networkAssignments", null, firebaseIdToken, ct).ConfigureAwait(false);
                await _rtdb.PutAsync<object?>($"drones/{escapedId}/network_assignments", null, firebaseIdToken, ct).ConfigureAwait(false);
            }
            catch { }
        }
    }
}

