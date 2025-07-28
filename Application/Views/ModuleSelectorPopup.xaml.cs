using System;
using System.Windows;

namespace DroneSurveillanceSystem.Views
{
    public partial class ModuleSelectorPopup : Window
    {
        public ModuleSelectorPopup()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
