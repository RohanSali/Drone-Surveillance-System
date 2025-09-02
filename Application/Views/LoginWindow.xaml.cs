using DroneSurveillanceSystem.Services;
using System;
using System.Windows;

namespace DroneSurveillanceSystem.Views
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;
        public bool IsAuthenticated { get; private set; }
        public bool IsGuestMode { get; private set; }

        public LoginWindow(AuthService authService)
        {
            InitializeComponent();
            _authService = authService;
            
            // Try silent sign-in first
            TrySilentSignIn();
        }

        private async void TrySilentSignIn()
        {
            try
            {
                MicrosoftSignInButton.IsEnabled = false;
                GmailSignInButton.IsEnabled = false;
                GuestButton.IsEnabled = false;
                
                var success = await _authService.SilentSignInAsync();
                if (success)
                {
                    IsAuthenticated = true;
                    try
                    {
                        DeviceDataManager.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                        NetworkService.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                    }
                    catch { }
                    
                    // Create and show the main window for silently authenticated users
                    try
                    {
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    catch (Exception mainEx)
                    {
                        MessageBox.Show($"Error opening main window: {mainEx.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        this.Close();
                    }
                }
            }
            catch (Exception)
            {
                // Silent sign-in failed, enable buttons
            }
            finally
            {
                MicrosoftSignInButton.IsEnabled = true;
                GmailSignInButton.IsEnabled = true;
                GuestButton.IsEnabled = true;
            }
        }

        private async void MicrosoftSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MicrosoftSignInButton.IsEnabled = false;
                GmailSignInButton.IsEnabled = false;
                GuestButton.IsEnabled = false;
                
                var success = await _authService.SignInAsync();
                if (success)
                {
                    IsAuthenticated = true;
                    try
                    {
                        DeviceDataManager.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                        NetworkService.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                    }
                    catch { }
                    
                    // Create and show the main window for authenticated users
                    try
                    {
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    catch (Exception mainEx)
                    {
                        MessageBox.Show($"Error opening main window: {mainEx.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-in failed: {ex.Message}", "Authentication Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MicrosoftSignInButton.IsEnabled = true;
                GmailSignInButton.IsEnabled = true;
                GuestButton.IsEnabled = true;
            }
        }

        private async void GmailSignInButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MicrosoftSignInButton.IsEnabled = false;
                GmailSignInButton.IsEnabled = false;
                GuestButton.IsEnabled = false;
                
                // Use the dedicated Gmail authentication method
                var success = await _authService.SignInWithGmailAsync();
                if (success)
                {
                    IsAuthenticated = true;
                    try
                    {
                        DeviceDataManager.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                        NetworkService.SetCurrentUser(_authService.CurrentUserEmail ?? _authService.CurrentUserName ?? "guest");
                    }
                    catch { }
                    
                    // Create and show the main window for authenticated users
                    try
                    {
                        var mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    catch (Exception mainEx)
                    {
                        MessageBox.Show($"Error opening main window: {mainEx.Message}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gmail sign-in failed: {ex.Message}", "Authentication Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                MicrosoftSignInButton.IsEnabled = true;
                GmailSignInButton.IsEnabled = true;
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
            
            // If neither authenticated nor guest mode, exit the application
            if (!IsAuthenticated && !IsGuestMode)
            {
                Application.Current.Shutdown();
            }
        }
    }
}
