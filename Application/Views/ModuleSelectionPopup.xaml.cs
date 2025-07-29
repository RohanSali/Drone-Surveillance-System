using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DroneSurveillanceSystem.Views
{
    public partial class ModuleSelectionPopup : Window
    {
        private string droneName = string.Empty;

        public ModuleSelectionPopup()
        {
            InitializeComponent();
        }

        public ModuleSelectionPopup(string droneName)
        {
            InitializeComponent();
            this.droneName = droneName;
            this.Title = $"Module Selection for {droneName}";
        }

        private void AddModulesButton_Click(object sender, RoutedEventArgs e)
        {
            // Get selected modules
            List<string> selectedModules = GetSelectedModules();

            if (selectedModules.Count == 0)
            {
                MessageBox.Show("Please select at least one module to add.", 
                              "No Modules Selected", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Warning);
                return;
            }

            // Show confirmation with selected modules
            string modulesList = string.Join("\n• ", selectedModules);
            string confirmationMessage = $"The following modules will be added to {droneName}:\n\n• {modulesList}\n\nProceed with installation?";

            MessageBoxResult result = MessageBox.Show(confirmationMessage, 
                                                    "Confirm Module Installation", 
                                                    MessageBoxButton.YesNo, 
                                                    MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Simulate module installation
                MessageBox.Show($"Successfully installed {selectedModules.Count} module(s) to {droneName}:\n\n• {modulesList}", 
                              "Installation Complete", 
                              MessageBoxButton.OK, 
                              MessageBoxImage.Information);

                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private List<string> GetSelectedModules()
        {
            List<string> selectedModules = new List<string>();

            // Find all CheckBox controls in the StackPanel
            var stackPanel = FindChild<StackPanel>(this);
            if (stackPanel != null)
            {
                foreach (CheckBox checkBox in stackPanel.Children.OfType<CheckBox>())
                {
                    if (checkBox.IsChecked == true && checkBox.Content != null)
                    {
                        selectedModules.Add(checkBox.Content.ToString() ?? string.Empty);
                    }
                }
            }

            return selectedModules;
        }

        // Helper method to find child controls
        private static T? FindChild<T>(DependencyObject? parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result)
                    return result;

                var childItem = FindChild<T>(child);
                if (childItem != null)
                    return childItem;
            }

            return null;
        }
    }
}
