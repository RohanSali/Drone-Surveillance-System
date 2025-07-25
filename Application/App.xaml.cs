using System;
using System.Windows;

namespace DroneSurveillanceSystem
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Handle any unhandled exceptions
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
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
