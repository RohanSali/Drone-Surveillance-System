using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DroneSurveillanceSystem.Services.Firebase
{
    internal class GoogleDesktopOAuthHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> GetGoogleIdTokenAsync(FirebaseAuthConfig config, CancellationToken ct)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.GoogleClientId))
                throw new InvalidOperationException("GoogleClientId is missing.");
            if (string.IsNullOrWhiteSpace(config.GoogleClientSecret))
                throw new InvalidOperationException("GoogleClientSecret is missing.");
            if (string.IsNullOrWhiteSpace(config.GoogleRedirectUri))
                throw new InvalidOperationException("GoogleRedirectUri is missing.");

            // Generate PKCE verifier/challenge.
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = CreateCodeChallenge(codeVerifier);

            // Simple CSRF protection.
            var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            // Authorization code flow for installed app using local redirect URI.
            var authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                "?response_type=code" +
                $"&client_id={Uri.EscapeDataString(config.GoogleClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(config.GoogleRedirectUri)}" +
                "&scope=" + Uri.EscapeDataString("openid email profile") +
                $"&state={Uri.EscapeDataString(state)}" +
                "&access_type=offline" +
                "&prompt=consent" +
                $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                "&code_challenge_method=S256";

            var listener = new HttpListener();
            listener.Prefixes.Add(config.GoogleRedirectUri);

            try
            {
                listener.Start();

                // Open browser for user login.
                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                // Wait for OAuth callback.
                var context = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
                var query = context.Request.QueryString;

                // Reply immediately so the browser doesn't hang.
                var responseHtml = "<html><body>You can close this window.</body></html>";
                var buffer = Encoding.UTF8.GetBytes(responseHtml);
                context.Response.ContentType = "text/html";
                context.Response.ContentLength64 = buffer.Length;
                await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);
                context.Response.OutputStream.Close();

                var returnedState = query["state"];
                if (!string.Equals(returnedState, state, StringComparison.Ordinal))
                    throw new InvalidOperationException("Google OAuth state validation failed.");

                var code = query["code"];
                var error = query["error"];
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException($"Google OAuth error: {error}");
                if (string.IsNullOrWhiteSpace(code))
                    throw new InvalidOperationException("Google OAuth callback did not include 'code'.");

                // Exchange authorization code for tokens (including id_token).
                var tokenUrl = "https://oauth2.googleapis.com/token";
                using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
                {
                    Content = new FormUrlEncodedContent(new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "code", code },
                        { "client_id", config.GoogleClientId },
                        { "client_secret", config.GoogleClientSecret },
                        { "redirect_uri", config.GoogleRedirectUri },
                        { "grant_type", "authorization_code" },
                        { "code_verifier", codeVerifier }
                    })
                };

                using var tokenResponse = await _httpClient.SendAsync(tokenRequest, ct).ConfigureAwait(false);
                var tokenBody = await tokenResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!tokenResponse.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Google token exchange failed: {(int)tokenResponse.StatusCode} - {tokenBody}");

                var tokenJson = JsonSerializer.Deserialize<GoogleTokenResponse>(tokenBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenJson == null || string.IsNullOrWhiteSpace(tokenJson.IdToken))
                    throw new InvalidOperationException($"Google token response did not contain id_token: {tokenBody}");

                return tokenJson.IdToken;
            }
            finally
            {
                try { listener.Stop(); } catch { }
                try { listener.Close(); } catch { }
            }
        }

        private static string GenerateCodeVerifier()
        {
            // RFC 7636 suggests 43-128 characters; use 64 bytes then base64url.
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Base64UrlEncode(bytes);
        }

        private static string CreateCodeChallenge(string codeVerifier)
        {
            var bytes = Encoding.ASCII.GetBytes(codeVerifier);
            var hash = SHA256.HashData(bytes);
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] input)
        {
            return Convert.ToBase64String(input)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private sealed class GoogleTokenResponse
        {
            [JsonPropertyName("id_token")]
            public string IdToken { get; set; } = string.Empty;

            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; } = string.Empty;

            [JsonPropertyName("refresh_token")]
            public string? RefreshToken { get; set; }
        }
    }
}

