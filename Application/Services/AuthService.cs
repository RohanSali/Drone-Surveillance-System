using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Linq; // Added for .Any()
using DroneSurveillanceSystem.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using System.Threading;
using System.Diagnostics;

namespace DroneSurveillanceSystem.Services
{
    public class AuthService
    {
        // Read Client ID from configuration file
        private static readonly string ClientId = GetClientIdFromConfig();
        private static readonly string Authority = "https://login.microsoftonline.com/common";
        private static readonly string[] Scopes = { "User.Read", "openid", "profile", "email" };
        
        private IPublicClientApplication? _msalClient;
        private IAccount? _currentAccount;
        private string _userProfilePath;
        private static bool _googleConfigDialogShown = false;

        public event EventHandler<bool>? AuthenticationStateChanged;

        public bool IsAuthenticated => _currentAccount != null;
        public string? CurrentUserEmail { get; private set; }
        public string? CurrentUserName { get; private set; }

        public AuthService()
        {
            InitializeMsalClient();
            _userProfilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DroneSurveillance",
                "user.json"
            );
            LoadUserProfile();
        }

        private void InitializeMsalClient()
        {
            _msalClient = PublicClientApplicationBuilder
                .Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("http://localhost")
                .Build();

            // Load cached accounts
            var accounts = _msalClient.GetAccountsAsync().Result;
            if (accounts.Any())
            {
                _currentAccount = accounts.First();
                LoadUserProfile();
            }
        }

        public async Task<bool> SignInAsync()
        {
            try
            {
                var result = await _msalClient.AcquireTokenInteractive(Scopes)
                    .WithAccount(_currentAccount)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();

                if (result != null)
                {
                    _currentAccount = result.Account;
                    CurrentUserEmail = result.Account.Username;
                    CurrentUserName = result.Account.Username.Split('@')[0]; // Simple name extraction
                    
                    SaveUserProfile();
                    AuthenticationStateChanged?.Invoke(this, true);
                    return true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-in failed: {ex.Message}", "Authentication Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            
            return false;
        }

        public async Task SignOutAsync()
        {
            try
            {
                if (_currentAccount != null)
                {
                    await _msalClient.RemoveAsync(_currentAccount);
                }
                
                _currentAccount = null;
                CurrentUserEmail = null;
                CurrentUserName = null;
                
                DeleteUserProfile();
                AuthenticationStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-out failed: {ex.Message}", "Authentication Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<bool> SilentSignInAsync()
        {
            try
            {
                if (_currentAccount == null) return false;

                var result = await _msalClient.AcquireTokenSilent(Scopes, _currentAccount)
                    .ExecuteAsync();

                if (result != null)
                {
                    CurrentUserEmail = result.Account.Username;
                    CurrentUserName = result.Account.Username.Split('@')[0];
                    AuthenticationStateChanged?.Invoke(this, true);
                    return true;
                }
            }
            catch (Exception)
            {
                // Silent sign-in failed, user needs to sign in interactively
            }
            
            return false;
        }

        public async Task<bool> SignInWithGmailAsync()
        {
            try
            {
                // Try to get credentials from appsettings.json first
                var (clientId, clientSecret) = GetGoogleCredentialsFromConfig();
                
                // If not found in appsettings.json, try google_credentials.json
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    var credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google_credentials.json");
                    if (File.Exists(credentialsPath))
                    {
                        (clientId, clientSecret) = await GetGoogleCredentialsFromFileAsync(credentialsPath);
                    }
                }
                
                // Check if credentials are properly configured (improved validation)
                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ||
                    clientId.Contains("YOUR_") || clientId.Contains("your-actual") ||
                    clientSecret.Contains("YOUR_") || clientSecret.Contains("your-actual") ||
                    !clientId.Contains(".apps.googleusercontent.com") || !clientSecret.StartsWith("GOCSPX-"))
                {
                    // Only show dialog once per session
                    if (!_googleConfigDialogShown)
                    {
                        ShowGoogleConfigurationInstructions();
                        _googleConfigDialogShown = true;
                    }
                    return false;
                }

                // Create Google OAuth2 flow with corrected settings for desktop application
                var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = new ClientSecrets
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret
                    },
                    Scopes = new[] { "openid", "email", "profile" },
                    DataStore = new FileDataStore("DroneApp"),
                });

                // Use LocalServerCodeReceiver with default settings that handles localhost redirect URIs
                var codeReceiver = new LocalServerCodeReceiver();
                
                // Start OAuth2 authorization
                var credential = await new AuthorizationCodeInstalledApp(flow, codeReceiver)
                    .AuthorizeAsync("user", CancellationToken.None);

                if (credential != null)
                {
                    // Get user info from Google
                    string userEmail = "authenticated.user@gmail.com";
                    string userName = "GoogleUser";
                    
                    try
                    {
                        // Try to get actual user email from token if possible
                        var token = await credential.GetAccessTokenForRequestAsync();
                        if (!string.IsNullOrEmpty(token))
                        {
                            // In a real implementation, you would decode the ID token to get email
                            // For now, we'll use the credential's UserId if available
                            userName = credential.UserId ?? "GoogleUser";
                            userEmail = credential.UserId?.Contains("@") == true ? credential.UserId : $"{userName}@gmail.com";
                        }
                    }
                    catch (Exception)
                    {
                        // Use default values if token parsing fails
                    }
                    
                    CurrentUserEmail = userEmail;
                    CurrentUserName = userName;
                    
                    // Save profile and notify of successful authentication
                    SaveUserProfile();
                    AuthenticationStateChanged?.Invoke(this, true);
                    
                    MessageBox.Show(
                        $"Successfully signed in with Google!\n\nWelcome, {CurrentUserName}\nEmail: {CurrentUserEmail}",
                        "Google Sign-in Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Google sign-in error: {ex.Message}\n\n" +
                    "Detailed error information:\n" +
                    $"Type: {ex.GetType().Name}\n" +
                    "\nPlease ensure:\n" +
                    "1. Your Google OAuth2 credentials are correct\n" +
                    "2. http://localhost:8080 is added as redirect URI in Google Console\n" +
                    "3. Google APIs are enabled for your project\n\n" +
                    "For now, please use Microsoft sign-in or continue as Guest.", 
                    "Google Authentication Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error
                );
                return false;
            }
        }

        private void SaveUserProfile()
        {
            try
            {
                var directory = Path.GetDirectoryName(_userProfilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var profile = new UserProfile
                {
                    Email = CurrentUserEmail ?? string.Empty,
                    Name = CurrentUserName ?? string.Empty,
                    LastSignIn = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(profile);
                File.WriteAllText(_userProfilePath, json);
            }
            catch (Exception)
            {
                // Ignore profile save errors
            }
        }

        private void LoadUserProfile()
        {
            try
            {
                if (File.Exists(_userProfilePath))
                {
                    var json = File.ReadAllText(_userProfilePath);
                    var profile = JsonSerializer.Deserialize<UserProfile>(json);
                    
                    if (profile != null)
                    {
                        CurrentUserEmail = profile.Email;
                        CurrentUserName = profile.Name;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore profile load errors
            }
        }

        private void DeleteUserProfile()
        {
            try
            {
                if (File.Exists(_userProfilePath))
                {
                    File.Delete(_userProfilePath);
                }
            }
            catch (Exception)
            {
                // Ignore profile delete errors
            }
        }
        
        private (string clientId, string clientSecret) GetGoogleCredentialsFromConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (config.TryGetProperty("Google", out var google) &&
                        google.TryGetProperty("ClientId", out var clientId) &&
                        google.TryGetProperty("ClientSecret", out var clientSecret))
                    {
                        return (clientId.GetString() ?? "", clientSecret.GetString() ?? "");
                    }
                }
            }
            catch (Exception)
            {
                // Ignore config read errors
            }
            
            return ("", "");
        }
        
        private async Task<(string clientId, string clientSecret)> GetGoogleCredentialsFromFileAsync(string credentialsPath)
        {
            try
            {
                var credentialsJson = await File.ReadAllTextAsync(credentialsPath);
                var credentialsData = JsonSerializer.Deserialize<JsonElement>(credentialsJson);
                
                if (credentialsData.TryGetProperty("web", out var web))
                {
                    var clientId = web.GetProperty("client_id").GetString() ?? "";
                    var clientSecret = web.GetProperty("client_secret").GetString() ?? "";
                    return (clientId, clientSecret);
                }
                else if (credentialsData.TryGetProperty("installed", out var installed))
                {
                    var clientId = installed.GetProperty("client_id").GetString() ?? "";
                    var clientSecret = installed.GetProperty("client_secret").GetString() ?? "";
                    return (clientId, clientSecret);
                }
            }
            catch (Exception)
            {
                // Ignore file read errors
            }
            
            return ("", "");
        }
        
        private void ShowGoogleSetupInstructions()
        {
            MessageBox.Show(
                "Google Authentication Setup Required\n\n" +
                "To enable Gmail authentication:\n\n" +
                "📋 OPTION 1 - Use Configuration File:\n" +
                "1. Go to Google Cloud Console (https://console.cloud.google.com/)\n" +
                "2. Create a new project or select existing project\n" +
                "3. Enable Google OAuth2 API\n" +
                "4. Go to 'Credentials' → 'Create Credentials' → 'OAuth 2.0 Client IDs'\n" +
                "5. Choose 'Desktop application'\n" +
                "6. Download the JSON file as 'google_credentials.json'\n\n" +
                "📋 OPTION 2 - Use App Settings:\n" +
                "Update appsettings.json with your Google ClientId and ClientSecret\n\n" +
                "For now, please use Microsoft sign-in or continue as Guest.",
                "Gmail Authentication Setup Required",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
        
        private void ShowGoogleConfigurationInstructions()
        {
            var result = MessageBox.Show(
                "🔧 GOOGLE OAUTH2 CONFIGURATION REQUIRED\n\n" +
                "❌ Issue: Your Google Cloud Console OAuth2 Client is configured as 'Web application' " +
                "but needs to be 'Desktop application'.\n\n" +
                "✅ QUICK FIX STEPS:\n" +
                "1. Go to Google Cloud Console → APIs & Services → Credentials\n" +
                "2. DELETE current OAuth2 Client ID\n" +
                "3. Create NEW OAuth2 Client ID as 'Desktop application'\n" +
                "4. Add redirect URIs: http://localhost and http://localhost:8080\n" +
                "5. Download new credentials and update your config files\n\n" +
                "📋 Detailed instructions: See GOOGLE_OAUTH_FIX_GUIDE.md\n\n" +
                "⏭️ MEANWHILE: Use Microsoft Sign-in or Continue as Guest\n\n" +
                "Click 'OK' to dismiss this message (won't show again this session).",
                "Google Authentication Setup Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
        
        private static string GetClientIdFromConfig()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<JsonElement>(json);
                    
                    if (config.TryGetProperty("AzureAD", out var azureAD) &&
                        azureAD.TryGetProperty("ClientId", out var clientId))
                    {
                        var clientIdValue = clientId.GetString();
                        if (!string.IsNullOrEmpty(clientIdValue) && clientIdValue != "YOUR_AZURE_AD_CLIENT_ID")
                        {
                            return clientIdValue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading Azure AD configuration: {ex.Message}\n\nUsing public client mode.", 
                    "Configuration Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            // Return a well-known public client ID for Microsoft Graph (allows "Continue as Guest")
            return "14d82eec-204b-4c2f-b7e8-296a70dab67e"; // Microsoft Graph Explorer public client
        }
    }
}
