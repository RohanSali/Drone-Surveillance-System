using DroneSurveillanceSystem.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseRtdbRestClient
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseAuthConfig _config;

        public FirebaseRtdbRestClient(HttpClient httpClient, FirebaseAuthConfig config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        private string BuildUrl(string relativePath, string firebaseIdToken)
        {
            var dbUrl = _config.FirebaseDatabaseUrl.TrimEnd('/');
            relativePath = relativePath.TrimStart('/');
            return $"{dbUrl}/{relativePath}.json?auth={Uri.EscapeDataString(firebaseIdToken)}";
        }

        public async Task<T?> GetAsync<T>(string relativePath, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
                throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            var url = BuildUrl(relativePath, firebaseIdToken);
            using var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // For missing nodes RTDB returns "null" with 200 OK.
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"RTDB read failed: {(int)response.StatusCode} - {body}");

            if (string.Equals(body?.Trim(), "null", StringComparison.OrdinalIgnoreCase))
                return default;

            return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        public async Task PutAsync<T>(string relativePath, T value, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
                throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            var url = BuildUrl(relativePath, firebaseIdToken);
            var json = JsonSerializer.Serialize(value);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"RTDB write failed: {(int)response.StatusCode} - {body}");
            }
        }

        // Uses REST: PUT https://<db>.firebaseio.com/users/{uid}.json?auth={firebaseIdToken}
        public async Task UpsertUserAsync(UserProfile user, string firebaseIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(firebaseIdToken))
                throw new ArgumentException("firebaseIdToken is required.", nameof(firebaseIdToken));

            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.Id))
                throw new ArgumentException("user.Id (uid) is required.", nameof(user));

            await PutAsync($"users/{Uri.EscapeDataString(user.Id)}", user, firebaseIdToken, ct).ConfigureAwait(false);
        }
    }
}

