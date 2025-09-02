using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DroneSurveillanceSystem.Services;

namespace DroneSurveillanceSystem.Views
{
    public partial class CctvDetailsPage : Window
    {
        private readonly UsbCctv _cctv;
        private readonly UsbCctvService _service;

        public CctvDetailsPage(UsbCctv cctv, UsbCctvService service)
        {
            InitializeComponent();
            _cctv = cctv;
            _service = service;
            // Header and identity
            CctvNameHeader.Text = _cctv.Name;
            CctvNameDisplay.Text = _cctv.Name;
            HeaderId.Text = $"ID: {_cctv.DeviceId}  Port: {_cctv.UsbPort}";
            UsbPortInfo.Text = $"USB Port: {_cctv.UsbPort}";

            // Status & details
            StatusText.Text = $"{_cctv.Status}";
            FirmwareInfo.Text = $"Firmware: {_cctv.FirmwareVersion}";
            ResolutionInfo.Text = $"Current: {_cctv.Resolution} @ {_cctv.FrameRate} FPS";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (await _service.ConnectAsync(_cctv.Name))
            {
                StatusText.Text = "Connected - Ready";
                FetchButton.IsEnabled = true;
            }
        }

        private async void FetchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show loading state
                FetchButton.IsEnabled = false;
                FetchButton.Content = "🔄 Fetching...";
                StatusText.Text = "Initializing CCTV configuration...";
                
                // Simulate fetch process
                await Task.Delay(1000);
                
                // Open the configuration form
                var configForm = new CctvDetailsForm(_cctv);
                configForm.ShowDialog();
                
                if (configForm.DialogResult == true && configForm.Configuration != null)
                {
                    // Update CCTV with the new configuration
                    var config = configForm.Configuration;
                    
                    // Update the CCTV object with new data
                    _cctv.Status = "Configured - Ready for Operation";
                    _cctv.Resolution = config.Resolution.Split(' ')[0]; // Extract resolution part
                    _cctv.FrameRate = config.FramesPerSecond;
                    
                    // Update UI with fetched/configured details
                    StatusText.Text = "Configuration Applied Successfully";
                    FirmwareInfo.Text = $"Firmware: {_cctv.FirmwareVersion} | Model: {config.Model}";
                    ResolutionInfo.Text = $"Current: {config.Resolution} @ {config.FramesPerSecond} FPS";
                    
                    // Update the header subtitle with configuration summary
                    HeaderId.Text = $"ID: {_cctv.DeviceId} | IP: {config.CameraIp} | Location: {config.Location}";
                    
                    // Show success status
                    var successBorder = new Border
                    {
                        Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28a745")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(15, 10, 15, 10),
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    
                    var successText = new TextBlock
                    {
                        Text = $"✅ CCTV Configured: {config.GetSummary()}",
                        Foreground = Brushes.White,
                        FontSize = 14,
                        FontWeight = FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    };
                    
                    successBorder.Child = successText;
                    
                    // Find the main stack panel and add success message
                    var mainStackPanel = FindChild<StackPanel>(this, "MainStackPanel");
                    if (mainStackPanel == null)
                    {
                        // If we can't find the specific panel, add to the border content
                        var border = FindChild<Border>(this, null);
                        if (border?.Child is StackPanel stackPanel)
                        {
                            stackPanel.Children.Add(successBorder);
                        }
                    }
                    else
                    {
                        mainStackPanel.Children.Add(successBorder);
                    }
                    
                    MessageBox.Show(
                        $"CCTV details fetched and configured successfully!\n\n" +
                        $"Configuration Summary:\n" +
                        $"• Camera: {config.Model}\n" +
                        $"• IP Address: {config.CameraIp}\n" +
                        $"• Location: {config.Location}\n" +
                        $"• Resolution: {config.Resolution}\n" +
                        $"• FPS: {config.FramesPerSecond}\n" +
                        $"• Night Vision: {(config.NightVisionEnabled ? "Enabled" : "Disabled")}\n" +
                        $"• Motion Detection: {(config.MotionDetectionEnabled ? "Enabled" : "Disabled")}",
                        "CCTV Configuration Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    StatusText.Text = "Configuration cancelled by user";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "Error during configuration";
                MessageBox.Show($"Error fetching CCTV details: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Restore button state
                FetchButton.Content = "📥 Fetch";
                FetchButton.IsEnabled = true;
            }
        }
        
        private T? FindChild<T>(DependencyObject parent, string? childName) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T && (string.IsNullOrEmpty(childName) || 
                    (child is FrameworkElement element && element.Name == childName)))
                {
                    return (T)child;
                }
                
                var childOfChild = FindChild<T>(child, childName);
                if (childOfChild != null)
                    return childOfChild;
            }
            
            return null;
        }
    }
}


