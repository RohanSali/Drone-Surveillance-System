using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    internal class FirebaseRestClient
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseAuthConfig _config;

        public FirebaseRestClient(HttpClient httpClient, FirebaseAuthConfig config)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<FirebaseSignInWithIdpResult> SignInWithGoogleIdTokenAsync(string googleIdToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(googleIdToken))
                throw new ArgumentException("googleIdToken is required.", nameof(googleIdToken));

            // https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=[API_KEY]
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={Uri.EscapeDataString(_config.FirebaseApiKey)}";

            var payload = new
            {
                postBody = $"id_token={googleIdToken}&providerId=google.com",
                requestUri = "http://localhost",
                returnIdpCredential = true,
                returnSecureToken = true
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Firebase Auth signInWithIdp failed: {(int)response.StatusCode} - {body}");

            var result = JsonSerializer.Deserialize<FirebaseSignInWithIdpResult>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || string.IsNullOrWhiteSpace(result.LocalId))
                throw new InvalidOperationException($"Firebase Auth signInWithIdp returned an unexpected response: {body}");

            return result;
        }

        public async Task<FirebaseRefreshResult> RefreshWithRefreshTokenAsync(string refreshToken, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("refreshToken is required.", nameof(refreshToken));

            // https://securetoken.googleapis.com/v1/token?key=[API_KEY]
            var url = $"https://securetoken.googleapis.com/v1/token?key={Uri.EscapeDataString(_config.FirebaseApiKey)}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                // securetoken endpoint expects x-www-form-urlencoded (not JSON)
                Content = new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
                {
                    { "grant_type", "refresh_token" },
                    { "refresh_token", refreshToken }
                })
            };

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Firebase token refresh failed: {(int)response.StatusCode} - {body}");

            var result = JsonSerializer.Deserialize<FirebaseRefreshResult>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null || string.IsNullOrWhiteSpace(result.UserId))
                throw new InvalidOperationException($"Firebase token refresh returned unexpected response: {body}");

            return result;
        }
    }

    internal class FirebaseSignInWithIdpResult
    {
        public string IdToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string LocalId { get; set; } = string.Empty; // uid
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    internal class FirebaseRefreshResult
    {
        // Response is snake_case; bind explicitly.
        [System.Text.Json.Serialization.JsonPropertyName("id_token")]
        public string IdToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
    }
}

