using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseClientRegistryService
    {
        private readonly FirebaseRtdbRestClient _rtdb;

        public FirebaseClientRegistryService(FirebaseRtdbRestClient rtdb)
        {
            _rtdb = rtdb ?? throw new ArgumentNullException(nameof(rtdb));
        }

        public async Task<string> GetOrCreateAppClientIdAsync(string firebaseUid, string email, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseUid)) throw new ArgumentException("firebaseUid is required.", nameof(firebaseUid));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            // Requirement: users/{firebaseUid}/id -> app_xxx
            var existingPath = $"users/{Uri.EscapeDataString(firebaseUid)}/id";
            var existing = await _rtdb.GetAsync<string>(existingPath, firebaseIdToken, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            // Create a new global app client id.
            var appId = GenerateAppClientId();

            // Required mapping: rtdb/clients/app_clients/{appId} -> FirebaseUid
            await _rtdb.PutAsync($"clients/app_clients/{Uri.EscapeDataString(appId)}", firebaseUid.Trim(), firebaseIdToken, ct).ConfigureAwait(false);

            // Persist on the user profile for future logins.
            await _rtdb.PutAsync(existingPath, appId, firebaseIdToken, ct).ConfigureAwait(false);

            return appId;
        }

        public async Task EnsureDroneClientAsync(string droneId, string droneName, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(droneId)) throw new ArgumentException("droneId is required.", nameof(droneId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            // Requirement: pool of unique drones (no user/app nesting)
            var path = $"clients/drone_clients/{Uri.EscapeDataString(droneId.Trim())}";
            var existing = await _rtdb.GetAsync<string>(path, firebaseIdToken, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing)) return; // already present

            var name = string.IsNullOrWhiteSpace(droneName) ? droneId.Trim() : droneName.Trim();
            await _rtdb.PutAsync(path, name, firebaseIdToken, ct).ConfigureAwait(false);
        }

        public async Task EnsureCctvClientAsync(string cctvId, string cctvName, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cctvId)) throw new ArgumentException("cctvId is required.", nameof(cctvId));
            if (string.IsNullOrWhiteSpace(firebaseIdToken)) throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            // Requirement: pool of unique CCTVs (no user/app nesting)
            var path = $"clients/cctv_clients/{Uri.EscapeDataString(cctvId.Trim())}";
            var existing = await _rtdb.GetAsync<string>(path, firebaseIdToken, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing)) return; // already present

            var name = string.IsNullOrWhiteSpace(cctvName) ? cctvId.Trim() : cctvName.Trim();
            await _rtdb.PutAsync(path, name, firebaseIdToken, ct).ConfigureAwait(false);
        }

        private static string GenerateAppClientId()
        {
            // "random character+symbols" but RTDB keys cannot contain . # $ [ ] /
            // so we use base64url (A-Z a-z 0-9 - _) which is safe for RTDB keys.
            var bytes = RandomNumberGenerator.GetBytes(18); // 24 chars base64url-ish
            var token = Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return $"app_{token}";
        }
    }
}

