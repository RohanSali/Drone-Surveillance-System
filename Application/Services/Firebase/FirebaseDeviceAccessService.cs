using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseDeviceAccessService
    {
        private readonly FirebaseRtdbRestClient _rtdb;
        private readonly FirebaseUserClientMappingService _mappingService;

        public FirebaseDeviceAccessService(FirebaseRtdbRestClient rtdb)
        {
            _rtdb = rtdb ?? throw new ArgumentNullException(nameof(rtdb));
            _mappingService = new FirebaseUserClientMappingService(_rtdb);
        }

        public async Task<AccessLockResult> LockMappedDevicesForAppAsync(
            string appClientId,
            string firebaseIdToken,
            CancellationToken ct)
        {
            var result = new AccessLockResult();
            if (string.IsNullOrWhiteSpace(appClientId)) return result;
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) return result;

            var droneMappings = await _mappingService.GetDroneMappingsAsync(appClientId, firebaseIdToken, ct).ConfigureAwait(false);
            var cctvMappings = await _mappingService.GetCctvMappingsAsync(appClientId, firebaseIdToken, ct).ConfigureAwait(false);

            var droneIds = droneMappings.Values
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cctvIds = cctvMappings.Values
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var droneId in droneIds)
            {
                var locked = await TryLockDroneAsync(droneId, appClientId, firebaseIdToken, ct).ConfigureAwait(false);
                if (locked) result.LockedDrones.Add(droneId);
                else result.DeniedDrones.Add(droneId);
            }

            foreach (var cctvId in cctvIds)
            {
                var locked = await TryLockCctvAsync(cctvId, appClientId, firebaseIdToken, ct).ConfigureAwait(false);
                if (locked) result.LockedCctvs.Add(cctvId);
                else result.DeniedCctvs.Add(cctvId);
            }

            return result;
        }

        public async Task<bool> TryLockDroneAsync(string droneId, string appClientId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(droneId)) return false;
            if (string.IsNullOrWhiteSpace(appClientId)) return false;
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) return false;

            var escapedId = Uri.EscapeDataString(droneId.Trim());
            var accessPath = $"drones/{escapedId}/device_accessing";

            var current = await _rtdb.GetAsync<string>(accessPath, firebaseIdToken, ct).ConfigureAwait(false);

            // Ensure older schema fields are removed.
            try
            {
                var escapedIdForCleanup = escapedId;
                await _rtdb.PutAsync<object?>($"drones/{escapedIdForCleanup}/networkAssignments", null, firebaseIdToken, ct).ConfigureAwait(false);
                await _rtdb.PutAsync<object?>($"drones/{escapedIdForCleanup}/network_assignments", null, firebaseIdToken, ct).ConfigureAwait(false);
            }
            catch { }

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current.Trim(), appClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _rtdb.PutAsync(accessPath, appClientId.Trim(), firebaseIdToken, ct).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<bool> TryLockCctvAsync(string cctvId, string appClientId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cctvId)) return false;
            if (string.IsNullOrWhiteSpace(appClientId)) return false;
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) return false;

            var escapedId = Uri.EscapeDataString(cctvId.Trim());
            var accessPath = $"cctvs/{escapedId}/device_accessing";

            var current = await _rtdb.GetAsync<string>(accessPath, firebaseIdToken, ct).ConfigureAwait(false);

            // Ensure older schema fields are removed.
            try
            {
                await _rtdb.PutAsync<object?>($"cctvs/{escapedId}/networkAssignments", null, firebaseIdToken, ct).ConfigureAwait(false);
                await _rtdb.PutAsync<object?>($"cctvs/{escapedId}/network_assignments", null, firebaseIdToken, ct).ConfigureAwait(false);
            }
            catch { }

            if (string.IsNullOrWhiteSpace(current) || string.Equals(current.Trim(), appClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _rtdb.PutAsync(accessPath, appClientId.Trim(), firebaseIdToken, ct).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task UnlockMappedDevicesForAppAsync(
            string appClientId,
            string firebaseIdToken,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(appClientId)) return;
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) return;

            var droneMappings = await _mappingService.GetDroneMappingsAsync(appClientId, firebaseIdToken, ct).ConfigureAwait(false);
            var cctvMappings = await _mappingService.GetCctvMappingsAsync(appClientId, firebaseIdToken, ct).ConfigureAwait(false);

            var droneIds = droneMappings.Values
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var cctvIds = cctvMappings.Values
                .Select(m => m.Id)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var droneId in droneIds)
            {
                await UnlockDroneIfLockedByAppAsync(droneId, appClientId, firebaseIdToken, ct).ConfigureAwait(false);
            }

            foreach (var cctvId in cctvIds)
            {
                await UnlockCctvIfLockedByAppAsync(cctvId, appClientId, firebaseIdToken, ct).ConfigureAwait(false);
            }
        }

        private async Task UnlockDroneIfLockedByAppAsync(string droneId, string appClientId, string firebaseIdToken, CancellationToken ct)
        {
            var escapedId = Uri.EscapeDataString(droneId.Trim());
            var accessPath = $"drones/{escapedId}/device_accessing";
            var current = await _rtdb.GetAsync<string>(accessPath, firebaseIdToken, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(current) && string.Equals(current.Trim(), appClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _rtdb.PutAsync<object?>(accessPath, null, firebaseIdToken, ct).ConfigureAwait(false);
            }
        }

        private async Task UnlockCctvIfLockedByAppAsync(string cctvId, string appClientId, string firebaseIdToken, CancellationToken ct)
        {
            var escapedId = Uri.EscapeDataString(cctvId.Trim());
            var accessPath = $"cctvs/{escapedId}/device_accessing";
            var current = await _rtdb.GetAsync<string>(accessPath, firebaseIdToken, ct).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(current) && string.Equals(current.Trim(), appClientId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                await _rtdb.PutAsync<object?>(accessPath, null, firebaseIdToken, ct).ConfigureAwait(false);
            }
        }
    }

    public class AccessLockResult
    {
        public List<string> LockedDrones { get; } = new List<string>();
        public List<string> DeniedDrones { get; } = new List<string>();
        public List<string> LockedCctvs { get; } = new List<string>();
        public List<string> DeniedCctvs { get; } = new List<string>();
    }
}

