using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Services
{
    public class UserProfileService : INotifyPropertyChanged
    {
        private static UserProfileService? _instance;
        public static UserProfileService Instance => _instance ??= new UserProfileService();

        private string _userName = "Guest";
        private string _userEmail = "";
        private string _userInitials = "G";
        private string _loginProvider = "Guest";
        private bool _isGuest = true;
        private bool _isAuthenticated = false;

        public string UserName
        {
            get => _userName;
            private set { _userName = value; OnPropertyChanged(); }
        }

        public string UserEmail
        {
            get => _userEmail;
            private set { _userEmail = value; OnPropertyChanged(); }
        }

        public string UserInitials
        {
            get => _userInitials;
            private set { _userInitials = value; OnPropertyChanged(); }
        }

        public string LoginProvider
        {
            get => _loginProvider;
            private set { _loginProvider = value; OnPropertyChanged(); }
        }

        public bool IsGuest
        {
            get => _isGuest;
            private set { _isGuest = value; OnPropertyChanged(); }
        }

        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            private set { _isAuthenticated = value; OnPropertyChanged(); }
        }

        public string DisplayInfo => IsGuest ? "" : (!string.IsNullOrEmpty(UserEmail) ? UserEmail : "No contact info");

        public bool ShowContactInfo => !IsGuest && !string.IsNullOrEmpty(UserEmail);

        private UserProfileService()
        {
            // Initialize with guest mode by default
            SetGuestMode();
        }

        public void UpdateFromAuthService(AuthService authService, string? explicitProvider = null)
        {
            System.Diagnostics.Debug.WriteLine($"UserProfileService: UpdateFromAuthService called");
            System.Diagnostics.Debug.WriteLine($"  - IsAuthenticated: {authService.IsAuthenticated}");
            System.Diagnostics.Debug.WriteLine($"  - CurrentUserName: {authService.CurrentUserName}");
            System.Diagnostics.Debug.WriteLine($"  - CurrentUserEmail: {authService.CurrentUserEmail}");
            System.Diagnostics.Debug.WriteLine($"  - ExplicitProvider: {explicitProvider}");
            
            if (authService.IsAuthenticated)
            {
                var name = authService.CurrentUserName ?? "Unknown";
                var email = authService.CurrentUserEmail ?? "";

                // Use explicit provider if provided, otherwise fall back to email domain detection
                string provider = explicitProvider ?? "Account";
                if (string.IsNullOrEmpty(explicitProvider))
                {
                    // Only use email domain detection as fallback when no explicit provider is given
                    if (!string.IsNullOrEmpty(email))
                    {
                        if (email.Contains("gmail.com") || email.Contains("googlemail.com") || 
                            email.Contains("@google.com") || email.EndsWith(".gmail.com"))
                        {
                            provider = "Google";
                        }
                        else if (email.Contains("outlook.com") || email.Contains("hotmail.com") || 
                                 email.Contains("live.com") || email.Contains("msn.com"))
                        {
                            provider = "Microsoft";
                        }
                        else
                        {
                            provider = "Account";
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"  - Final provider: {provider}");
                SetAuthenticatedUser(name, email, provider);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  - Setting guest mode (not authenticated)");
                SetGuestMode();
            }
        }

        public void SetAuthenticatedUser(string name, string email, string provider)
        {
            System.Diagnostics.Debug.WriteLine($"UserProfileService: SetAuthenticatedUser called");
            System.Diagnostics.Debug.WriteLine($"  - Name: '{name}'");
            System.Diagnostics.Debug.WriteLine($"  - Email: '{email}'");
            System.Diagnostics.Debug.WriteLine($"  - Provider: '{provider}'");
            
            UserName = string.IsNullOrWhiteSpace(name) ? "Unknown" : name.Trim();
            UserEmail = email?.Trim() ?? "";
            LoginProvider = provider;
            IsGuest = false;
            IsAuthenticated = true;
            
            // Generate initials from name
            UserInitials = GenerateInitials(UserName);
            
            System.Diagnostics.Debug.WriteLine($"  - Final UserName: '{UserName}'");
            System.Diagnostics.Debug.WriteLine($"  - Final UserEmail: '{UserEmail}'");
            System.Diagnostics.Debug.WriteLine($"  - Final UserInitials: '{UserInitials}'");
            System.Diagnostics.Debug.WriteLine($"  - Final LoginProvider: '{LoginProvider}'");
            
            OnPropertyChanged(nameof(DisplayInfo));
            OnPropertyChanged(nameof(ShowContactInfo));
        }

        public void SetGuestMode()
        {
            System.Diagnostics.Debug.WriteLine($"UserProfileService: SetGuestMode called");
            
            UserName = "Guest";
            UserEmail = "";
            LoginProvider = "Guest";
            IsGuest = true;
            IsAuthenticated = false;
            UserInitials = "G";
            
            System.Diagnostics.Debug.WriteLine($"  - Set to Guest mode successfully");
            
            OnPropertyChanged(nameof(DisplayInfo));
            OnPropertyChanged(nameof(ShowContactInfo));
        }

        private string GenerateInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "U"; // Unknown

            var parts = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 0)
                return "U";

            if (parts.Length == 1)
            {
                // Single name - take first character
                return parts[0].Substring(0, 1).ToUpper();
            }

            // Multiple names - take first character of first and last name
            return $"{parts[0].Substring(0, 1).ToUpper()}{parts[parts.Length - 1].Substring(0, 1).ToUpper()}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}