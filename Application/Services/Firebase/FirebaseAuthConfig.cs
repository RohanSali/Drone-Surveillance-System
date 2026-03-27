using System;
using System.IO;
using System.Text.Json;

namespace DroneSurveillanceSystem.Services.Firebase
{
    public class FirebaseAuthConfig
    {
        public string FirebaseApiKey { get; set; } = string.Empty;
        public string FirebaseDatabaseUrl { get; set; } = string.Empty; // e.g. https://<project-id>.firebaseio.com

        public string GoogleClientId { get; set; } = string.Empty;
        public string GoogleClientSecret { get; set; } = string.Empty;
        public string GoogleRedirectUri { get; set; } = "http://localhost:5005/"; // must match Google OAuth console

        public string UserRole { get; set; } = "User";

        public static FirebaseAuthConfig Load()
        {
            // Prefer environment variables so you don't have to add any secret files to git.
            var fromEnv = FromEnvironment();
            if (fromEnv != null) return fromEnv;

            // Fallback to local file.
            var fileName = "firebase.local.json";

            // Search common locations:
            // 1) Output folder (bin/...).
            // 2) Current working directory.
            // 3) Project root (walk up a few directories).
            var candidates = new System.Collections.Generic.List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName)
            };

            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var i = 0; i < 6; i++)
            {
                if (dir.Parent == null) break;
                candidates.Add(Path.Combine(dir.Parent.FullName, fileName));
                dir = dir.Parent;
            }

            var configPath = Array.Find(candidates.ToArray(), File.Exists);
            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new FileNotFoundException(
                    "Missing Firebase config. Create 'firebase.local.json' (repo root is fine) or set environment variables.",
                    fileName);
            }

            var json = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<FirebaseAuthConfig>(json, options);

            if (config == null || string.IsNullOrWhiteSpace(config.FirebaseApiKey) || string.IsNullOrWhiteSpace(config.FirebaseDatabaseUrl) ||
                string.IsNullOrWhiteSpace(config.GoogleClientId) || string.IsNullOrWhiteSpace(config.GoogleClientSecret) ||
                string.IsNullOrWhiteSpace(config.GoogleRedirectUri))
            {
                throw new InvalidOperationException("firebase.local.json is present but required fields are missing.");
            }

            return config;
        }

        private static FirebaseAuthConfig? FromEnvironment()
        {
            // Expected env vars (no secrets stored in repo).
            var apiKey = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
            var databaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL");
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
            var redirectUri = Environment.GetEnvironmentVariable("GOOGLE_REDIRECT_URI");
            var userRole = Environment.GetEnvironmentVariable("FIREBASE_USER_ROLE");

            if (string.IsNullOrWhiteSpace(apiKey) &&
                string.IsNullOrWhiteSpace(databaseUrl) &&
                string.IsNullOrWhiteSpace(clientId) &&
                string.IsNullOrWhiteSpace(clientSecret))
            {
                return null;
            }

            return new FirebaseAuthConfig
            {
                FirebaseApiKey = apiKey ?? "",
                FirebaseDatabaseUrl = databaseUrl ?? "",
                GoogleClientId = clientId ?? "",
                GoogleClientSecret = clientSecret ?? "",
                GoogleRedirectUri = string.IsNullOrWhiteSpace(redirectUri) ? "http://localhost:5005/" : redirectUri,
                UserRole = string.IsNullOrWhiteSpace(userRole) ? "User" : userRole
            };
        }
    }
}

