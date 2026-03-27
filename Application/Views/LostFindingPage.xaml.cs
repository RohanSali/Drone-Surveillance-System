using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DroneSurveillanceSystem.Services;
using Newtonsoft.Json;

namespace DroneSurveillanceSystem.Views
{
    public partial class LostFindingPage : UserControl, INotifyPropertyChanged
    {
        private string _selectedImagePath;
        private string _selectedImageBase64;
        private readonly ObservableCollection<PendingRequest> _pendingRequests;
        private PendingRequest? _selectedRequest;

        public ObservableCollection<PendingRequest> PendingRequests => _pendingRequests;
        public PendingRequest? SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                _selectedRequest = value;
                OnPropertyChanged();
                UpdateDisplayForSelectedRequest();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public LostFindingPage()
        {
            InitializeComponent();
            DataContext = this;
            
            _pendingRequests = new ObservableCollection<PendingRequest>();
            
            // Initialize LostFindingManager with the active user context (guest or signed-in Firebase user).
            LostFindingManager.Instance.SetCurrentUser(DeviceDataManager.GetCurrentUser());
            // Copy requests from manager to local collection
            foreach (var request in LostFindingManager.Instance.PendingRequests)
            {
                _pendingRequests.Add(request);
            }
            
            // Subscribe to WebSocket messages for responses
            ApiService.Instance.MessageReceived += OnWebSocketMessageReceived;
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Image of Lost Person",
                Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    _selectedImagePath = openFileDialog.FileName;
                    
                    // Load and display the image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_selectedImagePath);
                    bitmap.EndInit();
                    
                    UploadedImage.Source = bitmap;
                    NoImageText.Visibility = Visibility.Collapsed;
                    
                    // Convert image to base64
                    _selectedImageBase64 = ConvertImageToBase64(_selectedImagePath);
                    
                    // Enable send button
                    SendRequestButton.IsEnabled = true;
                    
                    // Clear previous results
                    ClearResults();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SendRequestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedImageBase64))
            {
                MessageBox.Show("Please select an image first.", "No Image Selected", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Generate unique name with timestamp
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var uniqueName = $"LostFinding_{timestamp}_{Guid.NewGuid().ToString("N")[..8]}";
                
                // Get actual app_id from ApiService
                var appId = ApiService.Instance.GetAppClientId();
                
                // Create the WebSocket message with actual app_id
                var message = new
                {
                    type = "alert_image",
                    app_id = appId,
                    data = new
                    {
                        found = 0,
                        name = uniqueName,
                        drone_id = "drone_001", // You can make this configurable
                        actual_image = _selectedImageBase64,
                        matched_frame = "",
                        location = new[] { 0, 0, 0 },
                        timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                    }
                };

                // Send message via WebSocket
                var jsonMessage = JsonConvert.SerializeObject(message);
                Console.WriteLine($"[LostFindingPage] Sending alert_image with app_id: {appId}");
                ApiService.Instance.SendMessage(jsonMessage);

                // Add to pending requests
                var pendingRequest = new PendingRequest
                {
                    Name = uniqueName,
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Status = "Pending",
                    StatusColor = "#FF9800",
                    ImageBase64 = _selectedImageBase64,
                    MatchScore = 0,
                    MatchedImageBase64 = ""
                };
                
                _pendingRequests.Add(pendingRequest);
                LostFindingManager.Instance.AddPendingRequest(pendingRequest);
                
                // Automatically select the newly added request (latest request)
                foreach (var req in _pendingRequests)
                {
                    req.IsSelected = false;
                }
                pendingRequest.IsSelected = true;
                SelectedRequest = pendingRequest;
                
                // Update UI
                StatusText.Text = "Request Sent";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
                
                MessageBox.Show($"Lost person search request sent successfully!\nRequest ID: {uniqueName}", 
                    "Request Sent", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending request: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnWebSocketMessageReceived(object sender, string message)
        {
            try
            {
                var messageObj = JsonConvert.DeserializeObject<dynamic>(message);
                var type = messageObj?.type?.ToString();
                
                if (type == "alert_image" || type == "alert_image_received")
                {
                    var data = messageObj?.data;
                    var found = (int)(data?.found ?? 0);
                    var name = data?.name?.ToString();
                    
                    if (found == 1 && !string.IsNullOrEmpty(name))
                    {
                        // Find the matching pending request
                        var matchingRequest = _pendingRequests.FirstOrDefault(r => r.Name == name);
                        if (matchingRequest != null)
                        {
                            // Update the request status and store results
                            matchingRequest.Status = "Matched";
                            matchingRequest.StatusColor = "#4CAF50";
                            
                            // Store matched image and score
                            var matchedFrame = data?.matched_frame?.ToString();
                            if (!string.IsNullOrEmpty(matchedFrame))
                            {
                                matchingRequest.MatchedImageBase64 = matchedFrame;
                                Console.WriteLine($"[LostFinding] Stored matched frame for {name}, length: {matchedFrame.Length}");
                            }
                            
                            // Properly fetch and convert score
                            var scoreValue = data?.score;
                            double score = 0;
                            if (scoreValue != null)
                            {
                                if (scoreValue is double d)
                                    score = d;
                                else if (scoreValue is float f)
                                    score = f;
                                else if (scoreValue is int i)
                                    score = i / 100.0; // Convert percentage to decimal
                                else if (double.TryParse(scoreValue.ToString(), out double parsed))
                                    score = parsed;
                            }
                            matchingRequest.MatchScore = score;
                            Console.WriteLine($"[LostFinding] Stored match score for {name}: {score} ({score:P1})");
                            
                            // Update the request in the manager
                            LostFindingManager.Instance.UpdatePendingRequest(matchingRequest);
                            
                            // Update UI on the main thread
                            Dispatcher.Invoke(() =>
                            {
                                UpdateResults(data, matchingRequest);
                                
                                // If this is the currently selected request, update the display
                                if (SelectedRequest == matchingRequest)
                                {
                                    UpdateDisplayForSelectedRequest();
                                }
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing WebSocket message: {ex.Message}");
            }
        }

        private void UpdateResults(dynamic data, PendingRequest request)
        {
            try
            {
                // Update matched image if available
                var matchedFrame = data?.matched_frame?.ToString();
                if (!string.IsNullOrEmpty(matchedFrame))
                {
                    var matchedImage = ConvertBase64ToImage(matchedFrame);
                    if (matchedImage != null)
                    {
                        MatchedImage.Source = matchedImage;
                        NoResultsText.Visibility = Visibility.Collapsed;
                        Console.WriteLine($"[LostFinding] Updated matched image display for {request.Name}");
                    }
                }
                
                // Update match score using the stored score from the request
                MatchScoreText.Text = $"{request.MatchScore:P1}";
                
                // Update status
                StatusText.Text = "Matched";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                
                Console.WriteLine($"[LostFinding] Updated results for {request.Name}: Score={request.MatchScore:P1}, Status=Matched");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LostFinding] Error updating results: {ex.Message}");
            }
        }

        private void ClearResults()
        {
            MatchedImage.Source = null;
            NoResultsText.Visibility = Visibility.Visible;
            MatchScoreText.Text = "0%";
            StatusText.Text = "Waiting...";
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800"));
        }

        private string ConvertImageToBase64(string imagePath)
        {
            try
            {
                var imageBytes = File.ReadAllBytes(imagePath);
                return Convert.ToBase64String(imageBytes);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error converting image to base64: {ex.Message}");
            }
        }

        private BitmapImage? ConvertBase64ToImage(string base64String)
        {
            try
            {
                var imageBytes = Convert.FromBase64String(base64String);
                var bitmap = new BitmapImage();
                
                using (var stream = new MemoryStream(imageBytes))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                
                return bitmap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting base64 to image: {ex.Message}");
                return null;
            }
        }

        private void PendingRequest_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is PendingRequest request)
            {
                // Deselect all other requests
                foreach (var req in _pendingRequests)
                {
                    req.IsSelected = false;
                }
                
                // Select the clicked request
                request.IsSelected = true;
                SelectedRequest = request;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Refresh all pending requests status
                foreach (var request in _pendingRequests)
                {
                    // You can add logic here to check for updates from the server
                    // For now, we'll just trigger a UI refresh
                    request.NotifyPropertyChanged(nameof(request.Status));
                    request.NotifyPropertyChanged(nameof(request.StatusColor));
                }
                
                // If there's a selected request, refresh its display
                if (SelectedRequest != null)
                {
                    UpdateDisplayForSelectedRequest();
                }
                
                // Show refresh feedback without changing the actual status
                var originalStatusText = StatusText.Text;
                var originalStatusColor = StatusText.Foreground;
                
                StatusText.Text = "Refreshed";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#17a2b8"));
                
                // Reset status after 2 seconds
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    StatusText.Text = originalStatusText;
                    StatusText.Foreground = originalStatusColor;
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDisplayForSelectedRequest()
        {
            if (SelectedRequest == null)
            {
                ClearResults();
                return;
            }

            try
            {
                // Update left panel with the selected request's image
                if (!string.IsNullOrEmpty(SelectedRequest.ImageBase64))
                {
                    var image = ConvertBase64ToImage(SelectedRequest.ImageBase64);
                    if (image != null)
                    {
                        UploadedImage.Source = image;
                        NoImageText.Visibility = Visibility.Collapsed;
                    }
                }

                // Update right panel with results if available
                if (SelectedRequest.Status == "Matched" && !string.IsNullOrEmpty(SelectedRequest.MatchedImageBase64))
                {
                    var matchedImage = ConvertBase64ToImage(SelectedRequest.MatchedImageBase64);
                    if (matchedImage != null)
                    {
                        MatchedImage.Source = matchedImage;
                        NoResultsText.Visibility = Visibility.Collapsed;
                        Console.WriteLine($"[LostFinding] Displayed matched image for selected request: {SelectedRequest.Name}");
                    }
                }
                else
                {
                    MatchedImage.Source = null;
                    NoResultsText.Visibility = Visibility.Visible;
                }

                // Update match score and status
                MatchScoreText.Text = $"{SelectedRequest.MatchScore:P1}";
                StatusText.Text = SelectedRequest.Status == "Matched" ? "Matched" : "Pending";
                StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                    SelectedRequest.Status == "Matched" ? "#4CAF50" : "#FF9800"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating display for selected request: {ex.Message}");
            }
        }

        private void DeleteRequest_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PendingRequest request)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the request '{request.Name}'?",
                    "Delete Request",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Remove from local collection
                    _pendingRequests.Remove(request);
                    
                    // Remove from manager
                    LostFindingManager.Instance.RemovePendingRequest(request);
                    
                    // If this was the selected request, clear selection
                    if (SelectedRequest == request)
                    {
                        SelectedRequest = null;
                        ClearResults();
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Unsubscribe from WebSocket messages
            ApiService.Instance.MessageReceived -= OnWebSocketMessageReceived;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? CloseRequested;
    }

    // Data model for pending requests
    public class PendingRequest : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _timestamp = string.Empty;
        private string _status = string.Empty;
        private string _statusColor = "#FF9800";
        private string _imageBase64 = string.Empty;
        private string _matchedImageBase64 = string.Empty;
        private double _matchScore = 0;
        private bool _isSelected = false;
        private string _userId = string.Empty;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public string Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public string StatusColor
        {
            get => _statusColor;
            set
            {
                _statusColor = value;
                OnPropertyChanged();
            }
        }

        public string ImageBase64
        {
            get => _imageBase64;
            set
            {
                _imageBase64 = value;
                OnPropertyChanged();
            }
        }

        public string MatchedImageBase64
        {
            get => _matchedImageBase64;
            set
            {
                _matchedImageBase64 = value;
                OnPropertyChanged();
            }
        }

        public double MatchScore
        {
            get => _matchScore;
            set
            {
                _matchScore = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string UserId
        {
            get => _userId;
            set
            {
                _userId = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        public void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
