using System;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using DroneSurveillanceSystem.Services;
using System.Linq;
using DroneSurveillanceSystem.Views;

namespace DroneSurveillanceSystem
{
    public partial class App : Application
    {
        private ApiService? _apiService;
        private AuthService? _authService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Initialize authentication service
            _authService = new AuthService();
            
            // Show login window first
            var loginWindow = new LoginWindow(_authService);
            loginWindow.ShowDialog();
            
            // Check authentication result
            if (!loginWindow.IsAuthenticated && !loginWindow.IsGuestMode)
            {
                // User cancelled login, exit application
                Shutdown();
                return;
            }
            
            try
            {
                // Reset the AlertManager instance to ensure completely fresh start
                AlertManager.ResetInstance();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not reset AlertManager: {ex.Message}");
            }
            
            // Clear any existing WebSocket debug logs
            var websocketLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "websocket_debug.log");
            if (File.Exists(websocketLogPath))
            {
                try
                {
                    File.Delete(websocketLogPath);
                    Console.WriteLine("WebSocket debug log cleared on startup.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing WebSocket debug log: {ex.Message}");
                }
            }
            
            try
            {
                // Handle any unhandled exceptions
                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
                DispatcherUnhandledException += App_DispatcherUnhandledException;
                
                // Enable API service for WebSocket communication with drone (needed for Lost Finding feature)
                _apiService = new ApiService();
                _apiService.AlertReceived += (sender, args) =>
                {
                    var alert = args.Alert;
                    if (alert != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var existing = AlertManager.Instance.ActiveAlerts.FirstOrDefault(a => a.Timestamp == alert.Timestamp);
                            if (existing == null)
                                AlertManager.Instance.ActiveAlerts.Insert(0, alert);
                            else
                                AlertManager.Instance.ActiveAlerts[AlertManager.Instance.ActiveAlerts.IndexOf(existing)] = alert;
                        });
                    }
                };
                
                // Start WebSocket connection asynchronously without blocking startup
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _apiService.StartWebSocketAsync();
                        Console.WriteLine("[App] WebSocket connection started successfully");
                    }
                    catch (Exception wsEx)
                    {
                        Console.WriteLine($"[App] WebSocket connection failed: {wsEx.Message}");
                        // Don't show error to user as this is not critical for basic app functionality
                    }
                });
                
                // MainWindow will be created by LoginWindow after authentication
                Console.WriteLine("Authentication flow completed, MainWindow will be created by LoginWindow");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during startup: {ex.Message}\n\nStackTrace: {ex.StackTrace}", 
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Console.WriteLine($"Startup error: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clear all active alerts when application exits
            try
            {
                AlertManager.Instance.ClearAllAlerts();
                Console.WriteLine("All active alerts cleared on application exit.");
                
                // Clear alert log file as well
                var alertLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert_log.txt");
                if (File.Exists(alertLogPath))
                {
                    File.Delete(alertLogPath);
                    Console.WriteLine("Alert log file cleared on application exit.");
                }
                
                // Clear alert image file as well
                var alertImagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "alert.jpg");
                if (File.Exists(alertImagePath))
                {
                    File.Delete(alertImagePath);
                    Console.WriteLine("Alert image file cleared on application exit.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing alerts on exit: {ex.Message}");
            }

            // Dispose of API service
            _apiService?.Dispose();

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}", 
                          "Drone Surveillance System Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception? ex = e.ExceptionObject as Exception;
            MessageBox.Show($"A critical error occurred: {ex?.Message}", 
                          "Critical Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
        }
    }
}
