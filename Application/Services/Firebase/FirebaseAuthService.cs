using DroneSurveillanceSystem.Models;
using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseAuthService
    {
        private FirebaseAuthConfig? _config;
        private readonly string _cachePath;
        private readonly HttpClient _httpClient;
        private FirebaseRestClient? _firebaseRestClient;
        private FirebaseRtdbRestClient? _rtdbRestClient;
        private FirebaseClientRegistryService? _clientRegistry;
        private readonly GoogleDesktopOAuthHelper _googleOAuthHelper;

        public FirebaseAuthService()
        {
            _cachePath = GetDefaultCachePath();
            _httpClient = new HttpClient();
            _googleOAuthHelper = new GoogleDesktopOAuthHelper();
        }

        private FirebaseAuthConfig EnsureConfigLoaded()
        {
            if (_config != null) return _config;
            _config = FirebaseAuthConfig.Load();
            _firebaseRestClient = new FirebaseRestClient(_httpClient, _config);
            _rtdbRestClient = new FirebaseRtdbRestClient(_httpClient, _config);
            _clientRegistry = new FirebaseClientRegistryService(_rtdbRestClient);
            return _config;
        }

        private static string GetDefaultCachePath()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DroneSurveillance");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "firebase_auth_cache.json");
        }

        public async Task<FirebaseUser?> SignInWithGoogleAsync(CancellationToken ct)
        {
            var config = EnsureConfigLoaded();

            var googleIdToken = await _googleOAuthHelper.GetGoogleIdTokenAsync(config, ct).ConfigureAwait(false);

            var firebaseSignIn = await _firebaseRestClient!.SignInWithGoogleIdTokenAsync(googleIdToken, ct)
                .ConfigureAwait(false);

            var user = new FirebaseUser
            {
                Uid = firebaseSignIn.LocalId,
                Email = firebaseSignIn.Email,
                DisplayName = firebaseSignIn.DisplayName,
                FirebaseIdToken = firebaseSignIn.IdToken,
                FirebaseRefreshToken = firebaseSignIn.RefreshToken
            };

            // Create global app client id on first-ever login, otherwise reuse existing.
            user.AppClientId = await _clientRegistry!.GetOrCreateAppClientIdAsync(
                user.Uid,
                user.Email,
                user.FirebaseIdToken,
                ct).ConfigureAwait(false);

            await UpsertUserToRtdbAsync(user, ct).ConfigureAwait(false);
            await SaveCacheAsync(user, ct).ConfigureAwait(false);

            return user;
        }

        public async Task<FirebaseUser?> TrySilentSignInAsync(CancellationToken ct)
        {
            var cache = await LoadCacheAsync(ct).ConfigureAwait(false);
            if (cache == null) return null;
            if (string.IsNullOrWhiteSpace(cache.FirebaseRefreshToken)) return null;

            EnsureConfigLoaded();
            var refreshed = await _firebaseRestClient!.RefreshWithRefreshTokenAsync(cache.FirebaseRefreshToken, ct).ConfigureAwait(false);

            var user = new FirebaseUser
            {
                Uid = refreshed.UserId,
                Email = cache.Email,
                DisplayName = cache.DisplayName,
                FirebaseIdToken = refreshed.IdToken,
                FirebaseRefreshToken = refreshed.RefreshToken
            };

            // Prefer cached app client id, otherwise lookup/create (should exist after first login).
            user.AppClientId = string.IsNullOrWhiteSpace(cache.AppClientId)
                ? await _clientRegistry!.GetOrCreateAppClientIdAsync(user.Uid, user.Email, user.FirebaseIdToken, ct).ConfigureAwait(false)
                : cache.AppClientId;

            await UpsertUserToRtdbAsync(user, ct).ConfigureAwait(false);
            cache.FirebaseRefreshToken = user.FirebaseRefreshToken;
            cache.AppClientId = user.AppClientId;
            cache.UpdatedAtUtc = DateTime.UtcNow;
            cache.DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? cache.DisplayName : user.DisplayName;
            await SaveCacheRawAsync(cache, ct).ConfigureAwait(false);

            return user;
        }

        public Task SignOutAsync()
        {
            try
            {
                if (File.Exists(_cachePath))
                {
                    File.Delete(_cachePath);
                }
            }
            catch
            {
                // Best-effort local cleanup.
            }

            // No server revoke here (REST revoke is optional). We'll rely on local cache removal.
            return Task.CompletedTask;
        }

        private async Task UpsertUserToRtdbAsync(FirebaseUser user, CancellationToken ct)
        {
            var config = EnsureConfigLoaded();
            // IMPORTANT: don't overwrite the whole users/{uid} node (it would remove users/{uid}/id).
            // Write fields individually.
            var uid = user.Uid?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(uid))
                throw new InvalidOperationException("Firebase UID is missing.");

            var userBase = $"users/{Uri.EscapeDataString(uid)}";

            // Requirement: users/{firebaseUid}/id -> app_xxx
            if (!string.IsNullOrWhiteSpace(user.AppClientId))
            {
                await _rtdbRestClient!.PutAsync($"{userBase}/id", user.AppClientId.Trim(), user.FirebaseIdToken, ct).ConfigureAwait(false);
            }

            await _rtdbRestClient!.PutAsync($"{userBase}/email", user.Email ?? "", user.FirebaseIdToken, ct).ConfigureAwait(false);
            var name = string.IsNullOrWhiteSpace(user.DisplayName) ? (user.Email?.Split('@')[0] ?? "") : user.DisplayName;
            await _rtdbRestClient!.PutAsync($"{userBase}/name", name ?? "", user.FirebaseIdToken, ct).ConfigureAwait(false);
            await _rtdbRestClient!.PutAsync($"{userBase}/lastSignIn", DateTime.UtcNow, user.FirebaseIdToken, ct).ConfigureAwait(false);
            await _rtdbRestClient!.PutAsync($"{userBase}/role", config.UserRole, user.FirebaseIdToken, ct).ConfigureAwait(false);
            await _rtdbRestClient!.PutAsync($"{userBase}/isActive", true, user.FirebaseIdToken, ct).ConfigureAwait(false);
        }

        private async Task SaveCacheAsync(FirebaseUser user, CancellationToken ct)
        {
            var cache = new FirebaseAuthCache
            {
                FirebaseRefreshToken = user.FirebaseRefreshToken,
                Uid = user.Uid,
                AppClientId = user.AppClientId,
                Email = user.Email ?? "",
                DisplayName = user.DisplayName ?? "",
                UpdatedAtUtc = DateTime.UtcNow
            };

            await SaveCacheRawAsync(cache, ct).ConfigureAwait(false);
        }

        private async Task SaveCacheRawAsync(FirebaseAuthCache cache, CancellationToken ct)
        {
            var dir = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(cache, options);
            await File.WriteAllTextAsync(_cachePath, json, ct).ConfigureAwait(false);
        }

        private async Task<FirebaseAuthCache?> LoadCacheAsync(CancellationToken ct)
        {
            try
            {
                if (!File.Exists(_cachePath)) return null;
                var json = await File.ReadAllTextAsync(_cachePath, ct).ConfigureAwait(false);
                return JsonSerializer.Deserialize<FirebaseAuthCache>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FirebaseAuthService: cache load failed: {ex.Message}");
                return null;
            }
        }
    }

    public class FirebaseUser
    {
        public string Uid { get; set; } = string.Empty;
        public string AppClientId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public string FirebaseIdToken { get; set; } = string.Empty;
        public string FirebaseRefreshToken { get; set; } = string.Empty;
    }
}

