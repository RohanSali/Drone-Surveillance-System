using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseCctvMetadataService
    {
        private readonly FirebaseRtdbRestClient _rtdb;

        public FirebaseCctvMetadataService(FirebaseRtdbRestClient rtdb)
        {
            _rtdb = rtdb ?? throw new ArgumentNullException(nameof(rtdb));
        }

        public async Task UpsertCctvMetadataAsync(
            UsbCctv cctv,
            IEnumerable<string>? networkAssignments,
            string firebaseIdToken,
            CancellationToken ct)
        {
            if (cctv == null) throw new ArgumentNullException(nameof(cctv));
            if (string.IsNullOrWhiteSpace(cctv.DeviceId))
                throw new ArgumentException("CCTV DeviceId is required.", nameof(cctv));
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
                throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));
            // networkAssignments are intentionally NOT persisted under /cctvs anymore.

            var id = cctv.DeviceId.Trim();
            var escapedId = Uri.EscapeDataString(id);

            // Granular writes so we do not overwrite fields like device_accessing.
            await _rtdb.PutAsync($"cctvs/{escapedId}/deviceId", cctv.DeviceId?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/name", cctv.Name?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/type", "CCTV", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/firmwareVersion", cctv.FirmwareVersion?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/usbPort", cctv.UsbPort?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/bluetoothMacAddress", cctv.BluetoothMacAddress?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/ipAddress", cctv.IpAddress?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/simType", cctv.SimType?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/resolution", cctv.Resolution?.Trim() ?? "", firebaseIdToken, ct).ConfigureAwait(false);
            await _rtdb.PutAsync($"cctvs/{escapedId}/frameRate", cctv.FrameRate, firebaseIdToken, ct).ConfigureAwait(false);

            // Best-effort cleanup of older schema.
            try
            {
                await _rtdb.PutAsync<object?>($"cctvs/{escapedId}/networkAssignments", null, firebaseIdToken, ct).ConfigureAwait(false);
                await _rtdb.PutAsync<object?>($"cctvs/{escapedId}/network_assignments", null, firebaseIdToken, ct).ConfigureAwait(false);
            }
            catch { }
        }
    }
}

