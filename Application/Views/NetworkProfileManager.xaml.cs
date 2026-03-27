using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using DroneSurveillanceSystem.Services;
using DroneSurveillanceSystem.Services.Firebase;

namespace DroneSurveillanceSystem.Views
{
    public partial class NetworkProfileManager : UserControl
    {
        private readonly NetworkService _networkService;
        private readonly DroneTrackingService _droneTrackingService;
        private readonly DeviceRegistryService _deviceRegistryService;
        private readonly Dictionary<string, DronePosition> _availableDroneByName;
        private readonly Dictionary<string, SurveillanceDevice> _availableCctvByName;
        private Network? _currentEditingNetwork;

        public NetworkProfileManager(NetworkService networkService, DroneTrackingService droneTrackingService)
        {
            InitializeComponent();
            _networkService = networkService;
            _droneTrackingService = droneTrackingService;
            _deviceRegistryService = new DeviceRegistryService();

            // Bind drones to combo box (only user-added drones from DeviceDataManager)
            var availableDrones = DeviceDataManager.GetAllDrones()
                .Select(d => new DronePosition { Id = d.DeviceId, Name = d.Name, Status = DroneFlightStatus.Grounded })
                .ToList();
            _availableDroneByName = availableDrones
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .ToDictionary(d => d.Name, d => d, StringComparer.OrdinalIgnoreCase);
            AvailableDronesComboBox.ItemsSource = availableDrones;
            AvailableDronesComboBox.DisplayMemberPath = "Name";

            // Bind CCTVs to combo box (only user-added CCTVs from DeviceDataManager)
            var availableCctvs = DeviceDataManager.GetAllCctvs()
                .Select(c => new SurveillanceDevice { Id = c.DeviceId, Name = c.Name, Type = DeviceType.CCTV })
                .ToList();
            _availableCctvByName = availableCctvs
                .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);
            AvailableCctvsComboBox.ItemsSource = availableCctvs;
            AvailableCctvsComboBox.DisplayMemberPath = "Name";

            // Initialize UI
            LoadNetworkProfiles();
            ClearEditor();
            
            // Ensure the editor panel is hidden initially
            NetworkEditorBorder.Visibility = Visibility.Collapsed;
        }

        private void LoadNetworkProfiles()
        {
            NetworkListPanel.Children.Clear();
            foreach (var network in _networkService.Networks)
            {
                var networkCard = CreateNetworkCard(network);
                NetworkListPanel.Children.Add(networkCard);
            }

            // Update status bar
            TotalNetworksText.Text = _networkService.Networks.Count.ToString();
            ActiveNetworksText.Text = _networkService.Networks.Count(n => n.Status == "Active").ToString();
        }

        private Border CreateNetworkCard(Network network)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Status indicator
            var statusIndicator = new System.Windows.Shapes.Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(network.StatusColor)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(statusIndicator, 0);
            grid.Children.Add(statusIndicator);

            // Network info
            var infoPanel = new StackPanel();
            
            var nameText = new TextBlock
            {
                Text = network.Name,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 5)
            };
            infoPanel.Children.Add(nameText);

            var descText = new TextBlock
            {
                Text = network.Description,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 5)
            };
            infoPanel.Children.Add(descText);

            var countsText = new TextBlock
            {
                Text = $"Drones: {network.Drones.Count} | CCTVs: {network.Cctvs.Count} | Region: {network.CoverageRegion}",
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                FontSize = 11
            };
            infoPanel.Children.Add(countsText);

            Grid.SetColumn(infoPanel, 1);
            grid.Children.Add(infoPanel);

            // Status text
            var statusText = new TextBlock
            {
                Text = network.Status,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(network.StatusColor)),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);

            card.Child = grid;

            // Bind network card with click event for editing
            card.MouseLeftButtonUp += (s, e) => LoadNetworkIntoEditor(network);

            return card;
        }

        private void ClearEditor()
        {
            _currentEditingNetwork = null;
            NetworkNameTextBox.Text = "";
            NetworkDescriptionTextBox.Text = "";
            CoverageRegionComboBox.SelectedIndex = 0;
            PriorityLevelComboBox.SelectedIndex = 1;
            OperationModeComboBox.SelectedIndex = 1;
            AutoActivateCheckBox.IsChecked = false;
            AlertNotificationCheckBox.IsChecked = true;
            AssignedDronesPanel.Children.Clear();
            AssignedCctvsPanel.Children.Clear();
            EditorHeaderText.Text = "⚙️ Network Configuration";
            EditorHeaderText.Foreground = System.Windows.Media.Brushes.White;
            StatusText.Text = "Ready - Select a network to edit or create a new one";
            StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            
            // Reset button states
            SaveNetworkButton.Content = "💾 Save";
            DeleteNetworkButton.IsEnabled = false;
            DeleteNetworkButton.Opacity = 0.3;
            DeleteNetworkButton.ToolTip = "No network selected for deletion";
            
            // Reset panel border
            NetworkEditorBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
            NetworkEditorBorder.BorderThickness = new Thickness(1, 1, 1, 1);
            
            // Hide the network editor panel with animation
            HideNetworkEditor();
        }

        private void CreateNetworkButton_Click(object sender, RoutedEventArgs e)
        {
            // Show the network editor panel with animation
            ShowNetworkEditor();
            
            // Clear editor fields for new network
            NetworkNameTextBox.Text = "";
            NetworkDescriptionTextBox.Text = "";
            CoverageRegionComboBox.SelectedIndex = 0; // Default to first item
            PriorityLevelComboBox.SelectedIndex = 1;   // Default to Medium
            OperationModeComboBox.SelectedIndex = 1;   // Default to Patrol Mode
            AutoActivateCheckBox.IsChecked = false;
            AlertNotificationCheckBox.IsChecked = true;
            AssignedDronesPanel.Children.Clear();
            AssignedCctvsPanel.Children.Clear();

            // Update editor header with more prominent styling
            EditorHeaderText.Text = "🆕 Create New Network";
            EditorHeaderText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ACF50"));


            // Set status text
            StatusText.Text = "Creating new network - Fill in the details and click Save";
            StatusText.Foreground = System.Windows.Media.Brushes.LightBlue;
            
            // Clear current editing network
            _currentEditingNetwork = null;

            // Focus on the first input field for better UX
            NetworkNameTextBox.Focus();

            // Highlight the right panel to draw attention
            NetworkEditorBorder.BorderBrush = System.Windows.Media.Brushes.LightGreen;
            NetworkEditorBorder.BorderThickness = new Thickness(2, 2, 2, 2);
            
            // Reset border after 3 seconds
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, args) =>
            {
                NetworkEditorBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
                NetworkEditorBorder.BorderThickness = new Thickness(1, 1, 1, 1);
                timer.Stop();
            };
            timer.Start();

            // Update button states
            SaveNetworkButton.Content = "💾 Create Network";
            SaveNetworkButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ACF50"));
            DeleteNetworkButton.IsEnabled = false;
            DeleteNetworkButton.Opacity = 0.3;
            DeleteNetworkButton.ToolTip = "No network selected for deletion";

        }

        private void LoadNetworkIntoEditor(Network network)
        {
            // Show the network editor panel with animation
            ShowNetworkEditor();
            
            _currentEditingNetwork = network;
            
            // Populate fields with selected network details
            NetworkNameTextBox.Text = network.Name;
            NetworkDescriptionTextBox.Text = network.Description;
            
            // Set ComboBox selections by content
            foreach (ComboBoxItem item in CoverageRegionComboBox.Items)
            {
                if (item.Content.ToString() == network.CoverageRegion)
                {
                    CoverageRegionComboBox.SelectedItem = item;
                    break;
                }
            }
            
            foreach (ComboBoxItem item in PriorityLevelComboBox.Items)
            {
                if (item.Content.ToString() == network.PriorityLevel)
                {
                    PriorityLevelComboBox.SelectedItem = item;
                    break;
                }
            }
            
            foreach (ComboBoxItem item in OperationModeComboBox.Items)
            {
                if (item.Content.ToString() == network.OperationMode)
                {
                    OperationModeComboBox.SelectedItem = item;
                    break;
                }
            }

            // Set checkboxes
            AutoActivateCheckBox.IsChecked = network.AutoActivate;
            AlertNotificationCheckBox.IsChecked = network.AlertNotifications;

            // Populate assigned drones
            AssignedDronesPanel.Children.Clear();
            foreach(var drone in network.Drones)
            {
                AssignedDronesPanel.Children.Add(CreateAssignedDeviceItem(drone.Name, drone.Id));
            }

            // Populate assigned CCTVs
            AssignedCctvsPanel.Children.Clear();
            foreach (var cam in network.Cctvs)
            {
                AssignedCctvsPanel.Children.Add(CreateAssignedDeviceItem(cam.Name, cam.Id));
            }

            // Update editor header with editing styling
            EditorHeaderText.Text = "✏️ Editing Network: " + network.Name;
            EditorHeaderText.Foreground = System.Windows.Media.Brushes.White;

            // Set status text
            StatusText.Text = "Editing network - Adjust details as necessary";
            StatusText.Foreground = System.Windows.Media.Brushes.White;

            // Reset button states for editing
            SaveNetworkButton.Content = "💾 Save Changes";
            DeleteNetworkButton.IsEnabled = true;
            DeleteNetworkButton.Opacity = 1.0;
            DeleteNetworkButton.ToolTip = $"Delete network '{network.Name}'";

            // Reset panel border
            NetworkEditorBorder.BorderBrush = System.Windows.Media.Brushes.Gray;
            NetworkEditorBorder.BorderThickness = new Thickness(1, 1, 1, 1);
        }

        private void AddDroneButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedDrone = AvailableDronesComboBox.SelectedItem as DronePosition;
            if (selectedDrone != null)
            {
                // Check duplicate by ID first, then by display name.
                var isAlreadyAssigned = AssignedDronesPanel.Children.OfType<Border>().Any(border =>
                    string.Equals(border.Tag as string, selectedDrone.Id, StringComparison.OrdinalIgnoreCase) ||
                    (border.Child is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault()?.Text == selectedDrone.Name));
                
                if (isAlreadyAssigned)
                {
                    MessageBox.Show($"Drone '{selectedDrone.Name}' is already assigned to this network.", "Duplicate Assignment", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AssignedDronesPanel.Children.Add(CreateAssignedDeviceItem(selectedDrone.Name, selectedDrone.Id));
            }
        }

        private void AddCctvButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedCctv = AvailableCctvsComboBox.SelectedItem as SurveillanceDevice;
            if (selectedCctv != null)
            {
                // Check if CCTV is already assigned to this network (prevent duplicates within same network)
                var isAlreadyAssigned = AssignedCctvsPanel.Children.OfType<Border>().Any(border =>
                    string.Equals(border.Tag as string, selectedCctv.Id, StringComparison.OrdinalIgnoreCase) ||
                    (border.Child is Grid grid && grid.Children.OfType<TextBlock>().FirstOrDefault()?.Text == selectedCctv.Name));
                
                if (isAlreadyAssigned)
                {
                    MessageBox.Show($"CCTV '{selectedCctv.Name}' is already assigned to this network.", "Duplicate Assignment", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AssignedCctvsPanel.Children.Add(CreateAssignedDeviceItem(selectedCctv.Name, selectedCctv.Id));
            }
        }

        private async void SaveNetworkButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(NetworkNameTextBox.Text))
            {
                StatusText.Text = "Error: Network name is required";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            var networkData = new Network
            {
                Name = NetworkNameTextBox.Text,
                Description = NetworkDescriptionTextBox.Text ?? "",
                Status = "Active", // Default status
                StatusColor = "#4ACF50", // Default color for Active
                IconColor = "#4ACF50",
                CoverageRegion = ((ComboBoxItem)CoverageRegionComboBox.SelectedItem)?.Content?.ToString() ?? "Urban Zone",
                PriorityLevel = ((ComboBoxItem)PriorityLevelComboBox.SelectedItem)?.Content?.ToString() ?? "Medium Priority",
                OperationMode = ((ComboBoxItem)OperationModeComboBox.SelectedItem)?.Content?.ToString() ?? "Patrol Mode",
                AutoActivate = AutoActivateCheckBox.IsChecked ?? false,
                AlertNotifications = AlertNotificationCheckBox.IsChecked ?? true,
                Drones = AssignedDronesPanel.Children.OfType<Border>()
                    .Select(border => 
                    {
                        var id = border.Tag as string;
                        var name = ExtractAssignedName(border);
                        if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name) && _availableDroneByName.TryGetValue(name, out var found))
                        {
                            id = found.Id;
                        }
                        return new DronePosition { Id = id ?? string.Empty, Name = name };
                    })
                    .Where(drone => !string.IsNullOrWhiteSpace(drone.Id) || !string.IsNullOrWhiteSpace(drone.Name))
                    .ToList(),
                Cctvs = AssignedCctvsPanel.Children.OfType<Border>()
                    .Select(border =>
                    {
                        var id = border.Tag as string;
                        var name = ExtractAssignedName(border);
                        if (string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name) && _availableCctvByName.TryGetValue(name, out var found))
                        {
                            id = found.Id;
                        }
                        return new SurveillanceDevice { Id = id ?? string.Empty, Name = name, Type = DeviceType.CCTV };
                    })
                    .Where(cam => !string.IsNullOrWhiteSpace(cam.Id) || !string.IsNullOrWhiteSpace(cam.Name))
                    .ToList()
            };

            bool isNewNetwork = _currentEditingNetwork == null;
            string successMessage = "";

            if (_currentEditingNetwork != null)
            {
                // Update existing network
                _networkService.UpdateNetwork(networkData);
                successMessage = "Network updated successfully";
            }
            else
            {
                // Add new network
                _networkService.AddNetwork(networkData);
                successMessage = "Network created successfully";
            }

            // Sync static device metadata (including network assignments) to RTDB.
            await SyncAllDroneMetadataToFirebaseAsync();
            await SyncAllCctvMetadataToFirebaseAsync();

            LoadNetworkProfiles();
            ClearEditor();
            
            // Hide the network editor panel after successful save
            HideNetworkEditor();
            
            // Show success message with appropriate styling
            StatusText.Text = successMessage + " - Total Networks: " + _networkService.Networks.Count;
            StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
            
            // Highlight the newly created/updated network in the list
            if (isNewNetwork)
            {
                // Find and highlight the newly created network card
                var newNetworkCard = NetworkListPanel.Children.OfType<Border>()
                    .FirstOrDefault(card => card.Child is Grid grid && 
                        grid.Children.OfType<StackPanel>()
                        .Any(sp => sp.Children.OfType<TextBlock>()
                        .Any(tb => tb.Text == networkData.Name)));
                
                if (newNetworkCard != null)
                {
                    // Temporarily highlight the new network card
                    newNetworkCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
                    
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, args) =>
                    {
                        newNetworkCard.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45));
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }

        private async System.Threading.Tasks.Task SyncAllDroneMetadataToFirebaseAsync()
        {
            try
            {
                var user = FirebaseSession.Current;
                if (user == null || string.IsNullOrWhiteSpace(user.FirebaseIdToken))
                    return;

                var config = FirebaseAuthConfig.Load();
                using var http = new System.Net.Http.HttpClient();
                var rtdb = new FirebaseRtdbRestClient(http, config);
                var metadataService = new FirebaseDroneMetadataService(rtdb);

                var allDrones = DeviceDataManager.GetAllDrones();
                foreach (var drone in allDrones)
                {
                    if (string.IsNullOrWhiteSpace(drone.DeviceId)) continue;

                    var assignments = _networkService.Networks
                        .Where(n => n.Drones.Any(d =>
                            (!string.IsNullOrWhiteSpace(d.Id) && d.Id.Equals(drone.DeviceId, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(d.Name) && d.Name.Equals(drone.Name, StringComparison.OrdinalIgnoreCase))))
                        .Select(n => n.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    await metadataService.UpsertDroneMetadataAsync(
                        drone,
                        assignments,
                        user.FirebaseIdToken,
                        System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drone metadata sync failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task SyncAllCctvMetadataToFirebaseAsync()
        {
            try
            {
                var user = FirebaseSession.Current;
                if (user == null || string.IsNullOrWhiteSpace(user.FirebaseIdToken))
                    return;

                var config = FirebaseAuthConfig.Load();
                using var http = new System.Net.Http.HttpClient();
                var rtdb = new FirebaseRtdbRestClient(http, config);
                var metadataService = new FirebaseCctvMetadataService(rtdb);

                var allCctvs = DeviceDataManager.GetAllCctvs();
                foreach (var cctv in allCctvs)
                {
                    if (string.IsNullOrWhiteSpace(cctv.DeviceId)) continue;

                    var assignments = _networkService.Networks
                        .Where(n => n.Cctvs.Any(c =>
                            (!string.IsNullOrWhiteSpace(c.Id) && c.Id.Equals(cctv.DeviceId, StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(c.Name) && c.Name.Equals(cctv.Name, StringComparison.OrdinalIgnoreCase))))
                        .Select(n => n.Name)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    await metadataService.UpsertCctvMetadataAsync(
                        cctv,
                        assignments,
                        user.FirebaseIdToken,
                        System.Threading.CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CCTV metadata sync failed: {ex.Message}");
            }
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e)
        {
            // Hide the network editor panel with animation
            HideNetworkEditor();
            
            // Simply reload network profiles to abandon current editing session
            LoadNetworkProfiles();
            StatusText.Text = "Edit cancelled - Current networks reloaded";
            StatusText.Foreground = System.Windows.Media.Brushes.White;
        }

        private void DeleteNetworkButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if we're editing an existing network
            if (_currentEditingNetwork == null)
            {
                StatusText.Text = "Error: No network selected for deletion";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            // Confirm deletion with user
            var result = MessageBox.Show(
                $"Are you sure you want to delete the network '{_currentEditingNetwork.Name}'?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Remove the network from the service
                var networkToRemove = _networkService.Networks.FirstOrDefault(n =>
                    (!string.IsNullOrWhiteSpace(_currentEditingNetwork.NetworkId) && n.NetworkId == _currentEditingNetwork.NetworkId) ||
                    n.Name == _currentEditingNetwork.Name);
                if (networkToRemove != null)
                {
                    _networkService.RemoveNetwork(networkToRemove);
                    LoadNetworkProfiles();
                    ClearEditor();
                    // Hide the network editor panel after successful deletion
                    HideNetworkEditor();
                    StatusText.Text = "Network deleted successfully - Total Networks: " + _networkService.Networks.Count;
                    StatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    StatusText.Text = "Error: Network not found for deletion";
                    StatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ShowNetworkEditor()
        {
            // Make the panel visible
            NetworkEditorBorder.Visibility = Visibility.Visible;
            
            // Create fade-in and slide-in animation
            var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            
            var slideInAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 50,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            
            // Apply animations
            NetworkEditorBorder.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            NetworkEditorTransform.BeginAnimation(TranslateTransform.XProperty, slideInAnimation);
        }

        private void HideNetworkEditor()
        {
            // Create fade-out and slide-out animation
            var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            
            var slideOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 50,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            
            // Apply animations
            NetworkEditorBorder.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
            NetworkEditorTransform.BeginAnimation(TranslateTransform.XProperty, slideOutAnimation);
            
            // Hide the panel after animation completes
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            timer.Tick += (s, args) =>
            {
                NetworkEditorBorder.Visibility = Visibility.Collapsed;
                timer.Stop();
            };
            timer.Start();
        }
        public event EventHandler? CloseRequested;

        private static string ExtractAssignedName(Border border)
        {
            if (border.Child is Grid grid)
            {
                return grid.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? string.Empty;
            }
            return string.Empty;
        }

        private static Border CreateAssignedDeviceItem(string name, string? deviceId)
        {
            var item = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 45, 45)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 2, 0, 2),
                Tag = deviceId ?? string.Empty
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = name,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            var removeButton = new Button
            {
                Content = "✖",
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Width = 24,
                Height = 24,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(5, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Grid.SetColumn(removeButton, 1);
            grid.Children.Add(removeButton);

            removeButton.Click += (s, args) => { if (item.Parent is Panel p) p.Children.Remove(item); };

            item.Child = grid;
            return item;
        }
    }
}
