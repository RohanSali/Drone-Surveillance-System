using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DroneSurveillanceSystem.Views
{
    public partial class ImgViewer : Window
    {
        private double _currentZoom = 1.0;
        private double _currentRotation = 0.0;
        private int _currentImageIndex = 1;
        private int _totalImages = 150;

        public ImgViewer()
        {
            InitializeComponent();
        }

        private void ImgViewer_Initialized(object sender, EventArgs e)
        {
            // Load a default placeholder or first surveillance image
            LoadImagePlaceholder();
            UpdateImageInfo();
        }

        private void LoadImagePlaceholder()
        {
            try
            {
                // Create a simple placeholder image if no actual image is available
                var bitmap = new WriteableBitmap(800, 600, 96, 96, System.Windows.Media.PixelFormats.Bgr32, null);
                
                // Fill with a dark blue color
                var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    pixels[i] = 80;     // Blue
                    pixels[i + 1] = 80; // Green
                    pixels[i + 2] = 120; // Red
                    pixels[i + 3] = 255; // Alpha
                }
                
                bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), pixels, bitmap.PixelWidth * 4, 0);
                MainImage.Source = bitmap;
            }
            catch
            {
                // If there's any issue, keep the XAML-defined placeholder
            }
        }

        private void UpdateImageInfo()
        {
            ImageIdText.Text = $"Image ID: IMG_{_currentImageIndex:D3}";
            ImageInfoText.Text = $"Resolution: 1920x1080 | Size: 2.4MB | Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            CurrentFrameText.Text = $"Frame {_currentImageIndex}";
            TotalFramesText.Text = _totalImages.ToString();
            TimestampText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PreviousImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex > 1)
            {
                _currentImageIndex--;
                UpdateImageInfo();
                RecordSlider.Value = _currentImageIndex;
            }
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex < _totalImages)
            {
                _currentImageIndex++;
                UpdateImageInfo();
                RecordSlider.Value = _currentImageIndex;
            }
        }

        private void ZoomInButton_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Min(_currentZoom * 1.2, 5.0);
            ImageScaleTransform.ScaleX = _currentZoom;
            ImageScaleTransform.ScaleY = _currentZoom;
        }

        private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
        {
            _currentZoom = Math.Max(_currentZoom / 1.2, 0.1);
            ImageScaleTransform.ScaleX = _currentZoom;
            ImageScaleTransform.ScaleY = _currentZoom;
        }

        private void RotateButton_Click(object sender, RoutedEventArgs e)
        {
            _currentRotation += 90;
            if (_currentRotation >= 360) _currentRotation = 0;
            ImageRotateTransform.Angle = _currentRotation;
        }

        private void RecordSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CurrentFrameText != null)
            {
                CurrentFrameText.Text = $"Frame {(int)e.NewValue}";
            }
        }

        private void OverlayControls_MouseEnter(object sender, MouseEventArgs e)
        {
            OverlayControls.Opacity = 1.0;
        }

        private void OverlayControls_MouseLeave(object sender, MouseEventArgs e)
        {
            OverlayControls.Opacity = 0.0;
        }
    }
}
