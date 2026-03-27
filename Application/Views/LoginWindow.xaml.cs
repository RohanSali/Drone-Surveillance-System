using DroneSurveillanceSystem.Services;
using DroneSurveillanceSystem.Services.Firebase;
using System;
using System.Windows;
using System.Threading;

namespace DroneSurveillanceSystem.Views
{
    public partial class LoginWindow : Window
    {
        public bool IsAuthenticated { get; private set; }
        public bool IsGuestMode { get; private set; }

        private readonly FirebaseAuthService _firebaseAuthService;
        private CancellationTokenSource? _silentSignInCts;

        public LoginWindow()
        {
            InitializeComponent();
            _firebaseAuthService = new FirebaseAuthService();

            // Attempt silent sign-in first (based on cached Firebase refresh token).
            TrySilentSignIn();
        }

        private async void TrySilentSignIn()
        {
            try
            {
                GoogleSignInButton.IsEnabled = false;
                GuestButton.IsEnabled = false;

                _silentSignInCts = new CancellationTokenSource();
                var user = await _firebaseAuthService.TrySilentSignInAsync(_silentSignInCts.Token);
                if (user == null) return;

                FirebaseSession.Set(user);
                IsAuthenticated = true;
                IsGuestMode = false;

                var userKey = string.IsNullOrWhiteSpace(user.AppClientId) ? "guest" : user.AppClientId;

                // Lock devices for this appId before loading device lists
                if (!string.IsNullOrWhiteSpace(user.AppClientId))
                {
                    var config = FirebaseAuthConfig.Load();
                    using var http = new System.Net.Http.HttpClient();
                    var rtdb = new FirebaseRtdbRestClient(http, config);
                    var mapping = new FirebaseUserClientMappingService(rtdb);
                    await mapping.BestEffortMigrateMappingsAsync(
                        user.Uid,
                        user.AppClientId,
                        user.FirebaseIdToken,
                        System.Threading.CancellationToken.None);
                    var access = new FirebaseDeviceAccessService(rtdb);
                    await access.LockMappedDevicesForAppAsync(user.AppClientId, user.FirebaseIdToken, CancellationToken.None);
                }

                DeviceDataManager.SetCurrentUser(userKey);
                NetworkService.SetCurrentUser(userKey);

                var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? "User" : user.DisplayName;
                var email = string.IsNullOrWhiteSpace(user.Email) ? "" : user.Email;
                UserProfileService.Instance.SetAuthenticatedUser(displayName, email, "Google");

                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                // Ignore silent auth failures; user can still sign in or use guest.
                System.Diagnostics.Debug.WriteLine($"Silent Firebase sign-in failed: {ex.Message}");
            }
            finally
            {
                GoogleSignInButton.IsEnabled = true;
                GuestButton.IsEnabled = true;
            }
        }

        private async void GoogleSignInButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            GoogleSignInButton.IsEnabled = false;
            GuestButton.IsEnabled = false;

            try
            {
                var user = await _firebaseAuthService.SignInWithGoogleAsync(CancellationToken.None);
                if (user == null) return;

                FirebaseSession.Set(user);
                IsAuthenticated = true;
                IsGuestMode = false;

                var userKey = string.IsNullOrWhiteSpace(user.AppClientId) ? "guest" : user.AppClientId;

                // Lock devices for this appId before loading device lists
                if (!string.IsNullOrWhiteSpace(user.AppClientId))
                {
                    var config = FirebaseAuthConfig.Load();
                    using var http = new System.Net.Http.HttpClient();
                    var rtdb = new FirebaseRtdbRestClient(http, config);
                    var mapping = new FirebaseUserClientMappingService(rtdb);
                    await mapping.BestEffortMigrateMappingsAsync(
                        user.Uid,
                        user.AppClientId,
                        user.FirebaseIdToken,
                        System.Threading.CancellationToken.None);
                    var access = new FirebaseDeviceAccessService(rtdb);
                    await access.LockMappedDevicesForAppAsync(user.AppClientId, user.FirebaseIdToken, CancellationToken.None);
                }

                DeviceDataManager.SetCurrentUser(userKey);
                NetworkService.SetCurrentUser(userKey);

                var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? "User" : user.DisplayName;
                var email = string.IsNullOrWhiteSpace(user.Email) ? "" : user.Email;
                UserProfileService.Instance.SetAuthenticatedUser(displayName, email, "Google");

                var mainWindow = new MainWindow();
                mainWindow.Show();
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Google Firebase sign-in failed: {ex.Message}", "Authentication Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                GoogleSignInButton.IsEnabled = true;
                GuestButton.IsEnabled = true;
            }
        }

        private void GuestButton_Click(object sender, RoutedEventArgs e)
        {
            IsGuestMode = true;
            try
            {
                DeviceDataManager.SetCurrentUser("guest");
                NetworkService.SetCurrentUser("guest");
                
                // Update UserProfileService for guest mode
                UserProfileService.Instance.SetGuestMode();
            }
            catch { }
            
            // Create and show the main window for guest mode
            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening main window: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                // Still close the login window even if main window fails
                this.Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            try
            {
                _silentSignInCts?.Cancel();
                _silentSignInCts?.Dispose();
            }
            catch { }
            
            // If neither authenticated nor guest mode, exit the application
            if (!IsAuthenticated && !IsGuestMode)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
