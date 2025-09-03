using System;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Linq;
using DroneSurveillanceSystem.Services;
using System.Windows.Threading;
using System.ComponentModel;
using Microsoft.Win32;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Views
{
    public partial class MonitoringAlertsPage : Window, INotifyPropertyChanged
    {
        // Show only alerts from user-added devices (not hardcoded ones)
        public ObservableCollection<AlertData> ActiveAlerts => new ObservableCollection<AlertData>(AlertManager.Instance.GetAllDeviceAlerts());
        private readonly LostFindingManager _lostFindingManager;
        
        // Filter properties
        private DeviceFilterType _currentFilter = DeviceFilterType.All;
        public ObservableCollection<DeviceDisplayItem> FilteredDevices { get; } = new ObservableCollection<DeviceDisplayItem>();
        public ObservableCollection<AlertData> FilteredAlerts { get; } = new ObservableCollection<AlertData>();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Properties that bind to the persistent manager
        public List<string> PendingRequests => _lostFindingManager.PendingRequests;
        public List<Services.LostFindingData> LostFindingData => _lostFindingManager.LostFindingData;
        public int PendingCount => _lostFindingManager.PendingCount;

        public MonitoringAlertsPage()
        {
            InitializeComponent();
            Console.WriteLine($"[MonitoringAlertsPage] 🏗️ MonitoringAlertsPage constructor called - Window created");
            
            // Initialize the persistent lost finding manager
            _lostFindingManager = LostFindingManager.Instance;
            
            // Set up data context
            DataContext = this;
            
            // Subscribe to alert updates and refresh the filtered view
            AlertManager.Instance.ActiveAlerts.CollectionChanged += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(ActiveAlerts));
                    ApplyFilter(); // Refresh filtered data when alerts change
                });
            };
            
            // Subscribe to lost finding manager updates
            _lostFindingManager.PropertyChanged += (sender, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(e.PropertyName);
                    if (e.PropertyName == nameof(PendingCount))
                    {
                        UpdatePendingStatusButton();
                    }
                });
            };
            
            Console.WriteLine($"[MonitoringAlertsPage] ✅ MonitoringAlertsPage initialization completed");
            // Initialize pending status button with current count
            UpdatePendingStatusButton();
            
            // Initialize filter
            UpdateFilterButtonText();
            ApplyFilter();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            Console.WriteLine($"[MonitoringAlertsPage] 📱 Window source initialized - Window is now active");
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            Console.WriteLine($"[MonitoringAlertsPage] 🎯 Window activated - Page is now in focus");
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            Console.WriteLine($"[MonitoringAlertsPage] 🔄 Window deactivated - Page lost focus");
        }

        public static bool IsMonitoringAlertsPageOpen()
        {
            foreach (Window window in System.Windows.Application.Current.Windows)
            {
                if (window is MonitoringAlertsPage)
                {
                    Console.WriteLine($"[MonitoringAlertsPage] ✅ MonitoringAlertsPage is currently open and available");
                    return true;
                }
            }
            Console.WriteLine($"[MonitoringAlertsPage] ❌ MonitoringAlertsPage is not currently open");
            return false;
        }

        private void Alert_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is string alertTag)
            {
                // Find the alert by tag (assume tag is alert id)
                var alert = AlertManager.Instance.ActiveAlerts.FirstOrDefault(a => a.Timestamp == alertTag);
                if (alert != null)
                {
                    var alertPopup = new AlertInfoPopup(alert);
                    alertPopup.Owner = this;
                    alertPopup.ShowDialog();
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            // Cycle through filter types
            _currentFilter = _currentFilter switch
            {
                DeviceFilterType.All => DeviceFilterType.DronesOnly,
                DeviceFilterType.DronesOnly => DeviceFilterType.CctvOnly,
                DeviceFilterType.CctvOnly => DeviceFilterType.All,
                _ => DeviceFilterType.All
            };

            // Update button text and apply filter
            UpdateFilterButtonText();
            ApplyFilter();
        }

        private void UpdateFilterButtonText()
        {
            FilterButton.Content = _currentFilter switch
            {
                DeviceFilterType.All => "🔍 All Devices",
                DeviceFilterType.DronesOnly => "🚁 Drones Only",
                DeviceFilterType.CctvOnly => "📷 CCTV Only",
                _ => "🔍 All Devices"
            };
        }

        private void ApplyFilter()
        {
            // Update devices list
            FilteredDevices.Clear();
            
            // Update title
            DevicesTitle.Text = _currentFilter switch
            {
                DeviceFilterType.All => "🚁📷 All Devices",
                DeviceFilterType.DronesOnly => "🚁 Active Drones",
                DeviceFilterType.CctvOnly => "📷 CCTV Cameras",
                _ => "🚁📷 All Devices"
            };

            // Add devices based on filter
            if (_currentFilter == DeviceFilterType.All || _currentFilter == DeviceFilterType.DronesOnly)
            {
                foreach (var drone in DeviceDataManager.GetAllDrones())
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = drone.Name,
                        Details = $"Device ID: {drone.DeviceId}",
                        AdditionalInfo = $"USB Port: {drone.UsbPort} | Firmware: {drone.FirmwareVersion}",
                        Status = drone.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusColor = drone.IsConnected ? "#88C999" : "#FF6B6B",
                        DeviceId = drone.DeviceId,
                        DeviceType = "Drone"
                    });
                }
            }

            if (_currentFilter == DeviceFilterType.All || _currentFilter == DeviceFilterType.CctvOnly)
            {
                foreach (var cctv in DeviceDataManager.GetAllCctvs())
                {
                    FilteredDevices.Add(new DeviceDisplayItem
                    {
                        Name = cctv.Name,
                        Details = $"Device ID: {cctv.DeviceId}",
                        AdditionalInfo = $"Resolution: {cctv.Resolution} | Frame Rate: {cctv.FrameRate}fps",
                        Status = cctv.IsConnected ? "CONNECTED" : "DISCONNECTED",
                        StatusColor = cctv.IsConnected ? "#88C999" : "#FF6B6B",
                        DeviceId = cctv.DeviceId,
                        DeviceType = "CCTV"
                    });
                }
            }

            // Update alerts list
            FilteredAlerts.Clear();
            var allAlerts = AlertManager.Instance.GetAllDeviceAlerts();
            
            foreach (var alert in allAlerts)
            {
                // Check if this alert should be shown based on current filter
                bool shouldShow = _currentFilter switch
                {
                    DeviceFilterType.All => true,
                    DeviceFilterType.DronesOnly => IsDroneDevice(alert.DroneId),
                    DeviceFilterType.CctvOnly => IsCctvDevice(alert.DroneId),
                    _ => true
                };

                if (shouldShow)
                {
                    FilteredAlerts.Add(alert);
                }
            }

            // Notify UI of changes
            OnPropertyChanged(nameof(FilteredDevices));
            OnPropertyChanged(nameof(FilteredAlerts));
        }

        private bool IsDroneDevice(string deviceId)
        {
            return DeviceDataManager.GetAllDrones().Any(d => d.DeviceId == deviceId);
        }

        private bool IsCctvDevice(string deviceId)
        {
            return DeviceDataManager.GetAllCctvs().Any(c => c.DeviceId == deviceId);
        }

        private void AcknowledgeAlerts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all active alerts
                AlertManager.Instance.ActiveAlerts.Clear();
                
                // Show success message
                MessageBox.Show("All alerts have been successfully acknowledged and cleared.", 
                              "Alerts Acknowledged", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);
                
                // Update the UI
                OnPropertyChanged(nameof(ActiveAlerts));
                ApplyFilter(); // Refresh filtered alerts
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error acknowledging alerts: {ex.Message}", 
                              "Error", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Error);
            }
        }

        private async void LostFindingButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Prompt user to select an image
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
            };
            if (openFileDialog.ShowDialog() != true)
                return;

            // 2. Read image as byte array and convert to base64
            byte[] imageBytes;
            string base64Image;
            try
            {
                imageBytes = System.IO.File.ReadAllBytes(openFileDialog.FileName);
                base64Image = Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to read image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3. Prepare WebSocket message (type 'alert_image' with required fields)
            string uniqueName = $"LostFinding_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            
            var wsMessage = new
            {
                type = "alert_image",
                data = new {
                    found = 0,
                    name = uniqueName,
                    drone_id = "drone_001",
                    actual_image = base64Image,
                    matched_frame = "", // Send empty string instead of the same image
                    location = new double[] { 0, 0, 0 },
                    timestamp = DateTime.UtcNow.ToString("o")
                }
            };
            string json = System.Text.Json.JsonSerializer.Serialize(wsMessage);

            try
            {
                var apiService = DroneSurveillanceSystem.Services.ApiService.Instance;
                if (apiService == null)
                {
                    MessageBox.Show("WebSocket service not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (apiService._client == null)
                {
                    await apiService.StartWebSocketAsync();
                }
                if (apiService._client != null && apiService._client.IsRunning)
                {
                    DroneSurveillanceSystem.Services.LostFindingManager.LogWebSocket("SENT", json);
                    await apiService._client.SendInstant(json);
                    Console.WriteLine($"[MonitoringAlertsPage] Sent alert_image request with name: {uniqueName}");
                    MessageBox.Show("Lost Finding alert image sent via WebSocket!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Add to pending requests and store data using persistent manager
                    string requestId = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    
                    var lostFindingItem = new Services.LostFindingData
                    {
                        RequestId = requestId,
                        ActualImageBase64 = base64Image,
                        MatchedImageBase64 = "",
                        Location = "",
                        Score = "",
                        Timestamp = DateTime.Now,
                        Name = uniqueName
                    };
                    
                    _lostFindingManager.AddPendingRequest(uniqueName, lostFindingItem);
                    
                    UpdatePendingStatusButton();
                    
                    // Show the Lost Finding section with only the actual image
                    ShowAllLostFindingSections();
                }
                else
                {
                    MessageBox.Show("WebSocket connection failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to send WebSocket message: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePendingStatusButton()
        {
            // Update the button with current count from the persistent manager
            PendingStatusButton.Visibility = Visibility.Visible;
            PendingStatusButton.Content = $"⏳{_lostFindingManager.PendingCount}";
        }

        private void ShowPendingStatus()
        {
            // Always show the button, just update the count
            PendingStatusButton.Visibility = Visibility.Visible;
            PendingStatusButton.Content = $"⏳{_lostFindingManager.PendingCount}";
        }

        private void PendingStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lostFindingManager.LostFindingData.Count > 0)
            {
                // Show all Lost Finding analysis sections
                ShowAllLostFindingSections();
                
                MessageBox.Show($"You have {_lostFindingManager.PendingCount} pending Lost Finding request(s) waiting for drone response.", 
                              "Pending Requests", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No pending Lost Finding requests.", "Pending Requests", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ShowAllLostFindingSections()
        {
            Console.WriteLine($"[MonitoringAlertsPage] 🔄 ShowAllLostFindingSections called");
            Console.WriteLine($"[MonitoringAlertsPage]   - Total data items: {_lostFindingManager.LostFindingData.Count}");
            Console.WriteLine($"[MonitoringAlertsPage]   - LostFindingSection visibility: {LostFindingSection.Visibility}");
            
            // Show the main Lost Finding section
            LostFindingSection.Visibility = Visibility.Visible;
            Console.WriteLine($"[MonitoringAlertsPage] ✅ Set LostFindingSection visibility to Visible");
            
            // Clear existing content and add all Lost Finding analyses
            ClearLostFindingContent();
            Console.WriteLine($"[MonitoringAlertsPage] ✅ Cleared existing content");
            
            // Add each Lost Finding analysis to the scrollable content
            foreach (var data in _lostFindingManager.LostFindingData)
            {
                Console.WriteLine($"[MonitoringAlertsPage] 📋 Adding analysis for request: {data.Name}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Has matched image: {!string.IsNullOrEmpty(data.MatchedImageBase64)}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Matched image length: {data.MatchedImageBase64?.Length ?? 0}");
                AddLostFindingAnalysis(data);
            }
            
            Console.WriteLine($"[MonitoringAlertsPage] ✅ Finished adding all analyses");
        }

        private void ClearLostFindingContent()
        {
            // Find the content area and clear it
            var contentPanel = LostFindingSection.FindName("LostFindingContentPanel") as StackPanel;
            if (contentPanel != null)
            {
                contentPanel.Children.Clear();
            }
        }

        private void AddLostFindingAnalysis(LostFindingData data)
        {
            // Find the content area
            var contentPanel = LostFindingSection.FindName("LostFindingContentPanel") as StackPanel;
            if (contentPanel == null) return;

            // Create a new analysis section
            var analysisSection = CreateLostFindingAnalysisSection(data);
            contentPanel.Children.Add(analysisSection);
        }

        private FrameworkElement CreateLostFindingAnalysisSection(LostFindingData data)
        {
            // Create a border for this analysis
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 10, 0, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header with timestamp
            var headerBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1e1e1e")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerText = new TextBlock
            {
                Text = $"🔍 Lost Finding Analysis - {data.Timestamp:HH:mm:ss}",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00d4ff")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var closeButton = new Button
            {
                Content = "✕",
                Width = 30,
                Height = 30,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#dc3545")),
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top
            };
            closeButton.Click += (s, e) => RemoveLostFindingAnalysis(border);

            headerGrid.Children.Add(headerText);
            Grid.SetColumn(headerText, 0);
            headerGrid.Children.Add(closeButton);
            Grid.SetColumn(closeButton, 1);

            headerBorder.Child = headerGrid;

            // Content area
            var contentStack = new StackPanel();

            // Image comparison grid
            var imageGrid = new Grid();
            imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            imageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left side - Actual Image
            var leftBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 10, 0)
            };

            var leftStack = new StackPanel();
            var leftTitle = new TextBlock
            {
                Text = "📷 Actual Image (Drone)",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00d4ff")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var leftImageBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1e1e1e")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            var actualImage = new Image
            {
                Height = 200,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Convert base64 to image
            if (!string.IsNullOrEmpty(data.ActualImageBase64))
            {
                try
                {
                    var actualBase64 = SanitizeBase64(data.ActualImageBase64);
                    byte[] imageBytes = Convert.FromBase64String(actualBase64);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(imageBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    actualImage.Source = bitmap;
                }
                catch (Exception)
                {
                    // Handle image conversion error
                    var errorText = new TextBlock
                    {
                        Text = "Image Error",
                        Foreground = System.Windows.Media.Brushes.Red,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    leftImageBorder.Child = errorText;
                }
            }

            if (leftImageBorder.Child == null)
                leftImageBorder.Child = actualImage;

            leftStack.Children.Add(leftTitle);
            leftStack.Children.Add(leftImageBorder);
            leftBorder.Child = leftStack;

            // Right side - Matched Image
            var rightBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(10, 0, 0, 0)
            };

            var rightStack = new StackPanel();
            var rightTitle = new TextBlock
            {
                Text = "🎯 Matched Image (Application)",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00d4ff")),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };

            var rightImageBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1e1e1e")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            var rightStackInner = new StackPanel();
            var matchedImage = new Image
            {
                Height = 200,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            var noImageText = new TextBlock
            {
                Text = "No image found",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888")),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Height = 200
            };

            // Convert matched image if available
            if (!string.IsNullOrEmpty(data.MatchedImageBase64))
            {
                Console.WriteLine($"[MonitoringAlertsPage] 🖼️ Processing matched image for request: {data.Name}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Base64 length: {data.MatchedImageBase64.Length}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Base64 starts with: {data.MatchedImageBase64.Substring(0, Math.Min(50, data.MatchedImageBase64.Length))}...");
                
                try
                {
                    var matchedBase64 = SanitizeBase64(data.MatchedImageBase64);
                    byte[] imageBytes = Convert.FromBase64String(matchedBase64);
                    Console.WriteLine($"[MonitoringAlertsPage]   - Converted to {imageBytes.Length} bytes");
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new System.IO.MemoryStream(imageBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    
                    Console.WriteLine($"[MonitoringAlertsPage]   - Created BitmapImage successfully");
                    Console.WriteLine($"[MonitoringAlertsPage]   - Bitmap dimensions: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
                    
                    matchedImage.Source = bitmap;
                    matchedImage.Visibility = Visibility.Visible;
                    noImageText.Visibility = Visibility.Collapsed;
                    
                    Console.WriteLine($"[MonitoringAlertsPage] ✅ Successfully displayed matched image for request: {data.Name}");
                    Console.WriteLine($"[MonitoringAlertsPage]   - Image visibility: {matchedImage.Visibility}");
                    Console.WriteLine($"[MonitoringAlertsPage]   - No image text visibility: {noImageText.Visibility}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MonitoringAlertsPage] ❌ Error processing matched image for request {data.Name}: {ex.Message}");
                    Console.WriteLine($"[MonitoringAlertsPage]   - Stack trace: {ex.StackTrace}");
                    // Keep showing "No image found"
                }
            }
            else
            {
                Console.WriteLine($"[MonitoringAlertsPage] ⚠️ No matched image available for request: {data.Name}");
                Console.WriteLine($"[MonitoringAlertsPage]   - MatchedImageBase64 is null or empty");
            }

            rightStackInner.Children.Add(matchedImage);
            rightStackInner.Children.Add(noImageText);
            rightImageBorder.Child = rightStackInner;

            rightStack.Children.Add(rightTitle);
            rightStack.Children.Add(rightImageBorder);
            rightBorder.Child = rightStack;

            // Add images to grid
            imageGrid.Children.Add(leftBorder);
            Grid.SetColumn(leftBorder, 0);
            imageGrid.Children.Add(rightBorder);
            Grid.SetColumn(rightBorder, 1);

            // Information grid
            var infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.Margin = new Thickness(0, 15, 0, 0);

            // Location
            var locationBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 5, 0)
            };

            var locationStack = new StackPanel();
            var locationTitle = new TextBlock
            {
                Text = "📍 Location of Person",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ff6b6b")),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var locationText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(data.Location) ? "—" : data.Location,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            locationStack.Children.Add(locationTitle);
            locationStack.Children.Add(locationText);
            locationBorder.Child = locationStack;

            // Score
            var scoreBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333")),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Margin = new Thickness(5, 0, 0, 0)
            };

            var scoreStack = new StackPanel();
            var scoreTitle = new TextBlock
            {
                Text = "🎯 Match Score",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ff6b6b")),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var scoreText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(data.Score) ? "—" : data.Score,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            scoreStack.Children.Add(scoreTitle);
            scoreStack.Children.Add(scoreText);
            scoreBorder.Child = scoreStack;

            // Add info to grid
            infoGrid.Children.Add(locationBorder);
            Grid.SetColumn(locationBorder, 0);
            infoGrid.Children.Add(scoreBorder);
            Grid.SetColumn(scoreBorder, 1);

            // Add all content
            contentStack.Children.Add(imageGrid);
            contentStack.Children.Add(infoGrid);

            grid.Children.Add(headerBorder);
            Grid.SetRow(headerBorder, 0);
            grid.Children.Add(contentStack);
            Grid.SetRow(contentStack, 1);

            border.Child = grid;
            return border;
        }

        private static string SanitizeBase64(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            // Strip common data URI prefixes
            const string pngPrefix = "data:image/png;base64,";
            const string jpgPrefix = "data:image/jpeg;base64,";
            const string jpegPrefix = "data:image/jpg;base64,";
            const string webpPrefix = "data:image/webp;base64,";
            if (input.StartsWith(pngPrefix, StringComparison.OrdinalIgnoreCase)) return input.Substring(pngPrefix.Length);
            if (input.StartsWith(jpgPrefix, StringComparison.OrdinalIgnoreCase)) return input.Substring(jpgPrefix.Length);
            if (input.StartsWith(jpegPrefix, StringComparison.OrdinalIgnoreCase)) return input.Substring(jpegPrefix.Length);
            if (input.StartsWith(webpPrefix, StringComparison.OrdinalIgnoreCase)) return input.Substring(webpPrefix.Length);
            // Trim whitespace/newlines just in case
            return input.Trim();
        }

        private void RemoveLostFindingAnalysis(Border analysisBorder)
        {
            // Find the content panel and remove this analysis
            var contentPanel = LostFindingSection.FindName("LostFindingContentPanel") as StackPanel;
            if (contentPanel != null)
            {
                contentPanel.Children.Remove(analysisBorder);
            }
            
            // Also remove the data from the manager to update pending count
            // Extract the name from the header text to identify which analysis to remove
            if (analysisBorder.Child is Grid grid && grid.Children.Count > 0)
            {
                if (grid.Children[0] is Border headerBorder && headerBorder.Child is Grid headerGrid)
                {
                    if (headerGrid.Children[0] is TextBlock headerText)
                    {
                        // Extract name from header text like "🔍 Lost Finding Analysis - 23:36:33"
                        string headerTextContent = headerText.Text;
                        if (headerTextContent.Contains(" - "))
                        {
                            string timestamp = headerTextContent.Split(" - ").Last();
                            // Find the corresponding data by timestamp
                            var dataToRemove = _lostFindingManager.LostFindingData
                                .FirstOrDefault(d => d.Timestamp.ToString("HH:mm:ss") == timestamp);
                            if (dataToRemove != null)
                            {
                                _lostFindingManager.RemoveLostFindingData(dataToRemove.Name);
                                Console.WriteLine($"[MonitoringAlertsPage] Removed Lost Finding analysis: {dataToRemove.Name}");
                            }
                        }
                    }
                }
            }
        }

        private void CloseLostFindingSection_Click(object sender, RoutedEventArgs e)
        {
            LostFindingSection.Visibility = Visibility.Collapsed;
        }

        public void HandleLostFindingResponse(string actualImageBase64, string matchedImageBase64, string location, string score, int found, string name = "")
        {
            // This method will be called when we receive a response from the drone
            Dispatcher.Invoke(() =>
            {
                Console.WriteLine($"[MonitoringAlertsPage] 🔍 Handling lost finding response:");
                Console.WriteLine($"[MonitoringAlertsPage]   - Name: '{name}'");
                Console.WriteLine($"[MonitoringAlertsPage]   - Found: {found}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Matched image length: {matchedImageBase64?.Length ?? 0}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Actual image length: {actualImageBase64?.Length ?? 0}");
                Console.WriteLine($"[MonitoringAlertsPage]   - Total lost finding data items: {_lostFindingManager.LostFindingData.Count}");
                Console.WriteLine($"[MonitoringAlertsPage]   - LostFindingSection current visibility: {LostFindingSection.Visibility}");
                
                // Use the persistent manager to handle the response
                _lostFindingManager.HandleResponse(name, matchedImageBase64 ?? "", location ?? "", score ?? "", found);
                
                // Always show the LostFindingSection when we receive a response
                Console.WriteLine($"[MonitoringAlertsPage] 📺 Making LostFindingSection visible");
                LostFindingSection.Visibility = Visibility.Visible;
                
                // Force refresh the display to show updated data
                Console.WriteLine($"[MonitoringAlertsPage] 🔄 Force refreshing display...");
                ShowAllLostFindingSections();
                
                // Show a notification to the user
                if (found == 1)
                {
                    MessageBox.Show(
                        $"Lost person found!\n\nName: {name}\nLocation: {location}\nScore: {score}%\n\nCheck the Lost Finding section for details.",
                        "Person Found!",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    MessageBox.Show(
                        $"Search completed for: {name}\n\nNo match found in the current scan area.\nThe drone will continue searching.",
                        "Search Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
            });
        }

        // Simple test method to manually trigger a response (for debugging)
        public void TestManualResponse()
        {
            Console.WriteLine($"[MonitoringAlertsPage] 🧪 Manual test triggered");
            
            if (_lostFindingManager.LostFindingData.Count > 0)
            {
                var latestData = _lostFindingManager.LostFindingData[_lostFindingManager.LostFindingData.Count - 1];
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 Testing with latest data: {latestData.Name}");
                
                // Create a simple test image (1x1 pixel)
                string testImage = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
                
                // Simulate the correct alert_image_received message structure
                var testMessage = new
                {
                    type = "alert_image_received",
                    alert_image = new
                    {
                        found = 1,
                        name = latestData.Name,
                        drone_id = "drone_001",
                        actual_image = latestData.ActualImageBase64,
                        matched_frame = testImage,
                        location = new double[] { 0, 0, 0 },
                        timestamp = DateTime.UtcNow.ToString("o"),
                        score = "95.5"
                    }
                };
                
                string testJson = System.Text.Json.JsonSerializer.Serialize(testMessage);
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 Simulated alert_image_received message: {testJson}");
                
                // Call the ApiService to handle this message
                var apiService = new DroneSurveillanceSystem.Services.ApiService();
                apiService.HandleMessage(testJson);
            }
            else
            {
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 No data available for testing");
            }
        }

        // Debug method to test image display logic
        public void DebugImageDisplay()
        {
            Console.WriteLine($"[MonitoringAlertsPage] 🔧 Debug image display called");
            
            if (_lostFindingManager.LostFindingData.Count > 0)
            {
                var latestData = _lostFindingManager.LostFindingData[_lostFindingManager.LostFindingData.Count - 1];
                Console.WriteLine($"[MonitoringAlertsPage] 🔧 Testing with data: {latestData.Name}");
                Console.WriteLine($"[MonitoringAlertsPage] 🔧 MatchedImageBase64 length: {latestData.MatchedImageBase64?.Length ?? 0}");
                
                // Force refresh the display
                ShowAllLostFindingSections();
            }
            else
            {
                Console.WriteLine($"[MonitoringAlertsPage] 🔧 No data available for debugging");
            }
        }

        // Test method to simulate a drone finding a person (for testing)
        private void TestDroneResponse_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: Simulating drone response");
            
            if (_lostFindingManager.LostFindingData.Count > 0)
            {
                var latestData = _lostFindingManager.LostFindingData[_lostFindingManager.LostFindingData.Count - 1];
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: Using latest data: {latestData.Name}");
                
                // Create a larger, more visible test image (50x50 pixel red square in PNG format)
                var testImageBytes = CreateTestImage();
                string testImage = Convert.ToBase64String(testImageBytes);
                
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: Generated test image, length: {testImage.Length}");
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: First 100 chars: {testImage.Substring(0, Math.Min(100, testImage.Length))}");
                
                // Call HandleLostFindingResponse directly
                HandleLostFindingResponse(
                    latestData.ActualImageBase64,
                    testImage,
                    "",
                    "89.5",
                    1, // found = 1 (person found)
                    latestData.Name
                );
                
                Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: Simulated response completed");
                
                // Force a UI refresh after a short delay to ensure data is updated
                Dispatcher.BeginInvoke(new Action(() => {
                    Console.WriteLine($"[MonitoringAlertsPage] 🧪 TEST: Delayed UI refresh triggered");
                    ShowAllLostFindingSections();
                }), DispatcherPriority.Background);
            }
            else
            {
                MessageBox.Show("No pending Lost Finding requests to test with.\n\nPlease submit a Lost Finding request first.", 
                              "Test Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private byte[] CreateTestImage()
        {
            // Create a simple 50x50 red square bitmap
            int width = 50;
            int height = 50;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];

            // Fill with solid red
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;       // Blue
                pixels[i + 1] = 0;   // Green
                pixels[i + 2] = 255; // Red
                pixels[i + 3] = 255; // Alpha
            }

            var bitmap = BitmapSource.Create(
                width, height, 96, 96,
                PixelFormats.Bgra32, null,
                pixels, stride);

            using (var stream = new System.IO.MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
                return stream.ToArray();
            }
        }
    }

    public enum DeviceFilterType
    {
        All,
        DronesOnly,
        CctvOnly
    }

    public class DeviceDisplayItem
    {
        public string Name { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string AdditionalInfo { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#88C999";
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
    }
}
