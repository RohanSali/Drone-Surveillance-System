using System;
using System.Windows;
using System.IO;
using DroneSurveillanceSystem.Services;
using System.Linq;

namespace DroneSurveillanceSystem
{
    public partial class App : Application
    {
        private ApiService? _apiService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Reset the AlertManager instance to ensure completely fresh start
            AlertManager.ResetInstance();
            
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
            
            // Handle any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
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
            _ = _apiService.StartWebSocketAsync();
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
