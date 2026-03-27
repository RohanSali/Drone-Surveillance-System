using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseUserClientMappingService
    {
        private readonly FirebaseRtdbRestClient _rtdb;

        public FirebaseUserClientMappingService(FirebaseRtdbRestClient rtdb)
        {
            _rtdb = rtdb ?? throw new ArgumentNullException(nameof(rtdb));
        }

        public async Task UpsertDroneMappingAsync(string userId, string droneId, string droneName, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(droneId)) throw new ArgumentException("droneId is required.", nameof(droneId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            var normalizedId = droneId.Trim();
            var payload = new UserClientMappingEntry
            {
                Id = normalizedId,
                Name = string.IsNullOrWhiteSpace(droneName) ? normalizedId : droneName.Trim()
            };

            await _rtdb.PutAsync(
                $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/drones/{Uri.EscapeDataString(normalizedId)}",
                payload,
                firebaseIdToken,
                ct).ConfigureAwait(false);
        }

        public async Task UpsertCctvMappingAsync(string userId, string cctvId, string cctvName, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(cctvId)) throw new ArgumentException("cctvId is required.", nameof(cctvId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            var normalizedId = cctvId.Trim();
            var payload = new UserClientMappingEntry
            {
                Id = normalizedId,
                Name = string.IsNullOrWhiteSpace(cctvName) ? normalizedId : cctvName.Trim()
            };

            await _rtdb.PutAsync(
                $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/cctvs/{Uri.EscapeDataString(normalizedId)}",
                payload,
                firebaseIdToken,
                ct).ConfigureAwait(false);
        }

        public async Task RemoveDroneMappingAsync(string userId, string droneId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(droneId) || string.IsNullOrWhiteSpace(firebaseIdToken))
                return;

            await _rtdb.PutAsync<object?>(
                $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/drones/{Uri.EscapeDataString(droneId.Trim())}",
                null,
                firebaseIdToken,
                ct).ConfigureAwait(false);
        }

        public async Task RemoveCctvMappingAsync(string userId, string cctvId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(cctvId) || string.IsNullOrWhiteSpace(firebaseIdToken))
                return;

            await _rtdb.PutAsync<object?>(
                $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/cctvs/{Uri.EscapeDataString(cctvId.Trim())}",
                null,
                firebaseIdToken,
                ct).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, UserClientMappingEntry>> GetDroneMappingsAsync(string userId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            return await _rtdb.GetAsync<Dictionary<string, UserClientMappingEntry>>(
                       $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/drones",
                       firebaseIdToken,
                       ct).ConfigureAwait(false)
                   ?? new Dictionary<string, UserClientMappingEntry>(StringComparer.OrdinalIgnoreCase);
        }

        public async Task<Dictionary<string, UserClientMappingEntry>> GetCctvMappingsAsync(string userId, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentException("userId is required.", nameof(userId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            return await _rtdb.GetAsync<Dictionary<string, UserClientMappingEntry>>(
                       $"user_client_mapping/{Uri.EscapeDataString(userId.Trim())}/cctvs",
                       firebaseIdToken,
                       ct).ConfigureAwait(false)
                   ?? new Dictionary<string, UserClientMappingEntry>(StringComparer.OrdinalIgnoreCase);
        }

        // Best-effort migration for older schema:
        // Previously mapping could have been written under Firebase uid; now it's written under app_xxx.
        public async Task BestEffortMigrateMappingsAsync(
            string? sourceUserId,
            string targetAppClientId,
            string firebaseIdToken,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(sourceUserId)) return;
            if (string.IsNullOrWhiteSpace(targetAppClientId)) return;
            if (string.Equals(sourceUserId.Trim(), targetAppClientId.Trim(), StringComparison.OrdinalIgnoreCase))
                return;

            var sourceDrones = await GetDroneMappingsAsync(sourceUserId, firebaseIdToken, ct).ConfigureAwait(false);
            if (sourceDrones.Count > 0)
            {
                foreach (var entry in sourceDrones.Values)
                {
                    if (string.IsNullOrWhiteSpace(entry.Id)) continue;
                    await UpsertDroneMappingAsync(
                        targetAppClientId,
                        entry.Id,
                        entry.Name,
                        firebaseIdToken,
                        ct).ConfigureAwait(false);
                }
            }

            var sourceCctvs = await GetCctvMappingsAsync(sourceUserId, firebaseIdToken, ct).ConfigureAwait(false);
            if (sourceCctvs.Count > 0)
            {
                foreach (var entry in sourceCctvs.Values)
                {
                    if (string.IsNullOrWhiteSpace(entry.Id)) continue;
                    await UpsertCctvMappingAsync(
                        targetAppClientId,
                        entry.Id,
                        entry.Name,
                        firebaseIdToken,
                        ct).ConfigureAwait(false);
                }
            }
        }
    }

    public class UserClientMappingEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
