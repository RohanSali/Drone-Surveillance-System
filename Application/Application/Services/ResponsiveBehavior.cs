using System;
using System.Windows;
using System.Windows.Media;

namespace DroneSurveillanceSystem.Services
{
	public static class ResponsiveBehavior
	{
		public static readonly DependencyProperty EnableAutoScaleProperty = DependencyProperty.RegisterAttached(
			"EnableAutoScale",
			typeof(bool),
			typeof(ResponsiveBehavior),
			new PropertyMetadata(false, OnEnableAutoScaleChanged));

		public static readonly DependencyProperty DesignWidthProperty = DependencyProperty.RegisterAttached(
			"DesignWidth",
			typeof(double),
			typeof(ResponsiveBehavior),
			new PropertyMetadata(1400.0, OnDesignSizeChanged));

		public static readonly DependencyProperty DesignHeightProperty = DependencyProperty.RegisterAttached(
			"DesignHeight",
			typeof(double),
			typeof(ResponsiveBehavior),
			new PropertyMetadata(900.0, OnDesignSizeChanged));

		public static readonly DependencyProperty MinScaleProperty = DependencyProperty.RegisterAttached(
			"MinScale",
			typeof(double),
			typeof(ResponsiveBehavior),
			new PropertyMetadata(0.7, OnDesignSizeChanged));

		public static readonly DependencyProperty MaxScaleProperty = DependencyProperty.RegisterAttached(
			"MaxScale",
			typeof(double),
			typeof(ResponsiveBehavior),
			new PropertyMetadata(1.5, OnDesignSizeChanged));

		public static bool GetEnableAutoScale(DependencyObject obj) => (bool)obj.GetValue(EnableAutoScaleProperty);
		public static void SetEnableAutoScale(DependencyObject obj, bool value) => obj.SetValue(EnableAutoScaleProperty, value);

		public static double GetDesignWidth(DependencyObject obj) => (double)obj.GetValue(DesignWidthProperty);
		public static void SetDesignWidth(DependencyObject obj, double value) => obj.SetValue(DesignWidthProperty, value);

		public static double GetDesignHeight(DependencyObject obj) => (double)obj.GetValue(DesignHeightProperty);
		public static void SetDesignHeight(DependencyObject obj, double value) => obj.SetValue(DesignHeightProperty, value);

		public static double GetMinScale(DependencyObject obj) => (double)obj.GetValue(MinScaleProperty);
		public static void SetMinScale(DependencyObject obj, double value) => obj.SetValue(MinScaleProperty, value);

		public static double GetMaxScale(DependencyObject obj) => (double)obj.GetValue(MaxScaleProperty);
		public static void SetMaxScale(DependencyObject obj, double value) => obj.SetValue(MaxScaleProperty, value);

		private static void OnEnableAutoScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not Window window)
			{
				return;
			}

			if ((bool)e.NewValue)
			{
				window.Loaded += WindowOnLoaded;
				window.SizeChanged += WindowOnSizeChanged;
			}
			else
			{
				window.Loaded -= WindowOnLoaded;
				window.SizeChanged -= WindowOnSizeChanged;
			}
		}

		private static void OnDesignSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Window window)
			{
				ApplyScale(window);
			}
		}

		private static void WindowOnLoaded(object sender, RoutedEventArgs e)
		{
			if (sender is Window window)
			{
				ApplyScale(window);
			}
		}

		private static void WindowOnSizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (sender is Window window)
			{
				ApplyScale(window);
			}
		}

		private static void ApplyScale(Window window)
		{
			if (window.Content is not FrameworkElement rootElement)
			{
				return;
			}

			double designWidth = GetDesignWidth(window);
			double designHeight = GetDesignHeight(window);
			double minScale = Math.Max(0.1, GetMinScale(window));
			double maxScale = Math.Max(minScale, GetMaxScale(window));

			// Compute scale based on available client size
			double availableWidth = Math.Max(1, window.ActualWidth);
			double availableHeight = Math.Max(1, window.ActualHeight);

			// Exclude window borders if possible
			if (window.Content is FrameworkElement contentElement)
			{
				availableWidth = Math.Max(1, contentElement.ActualWidth > 0 ? contentElement.ActualWidth : availableWidth);
				availableHeight = Math.Max(1, contentElement.ActualHeight > 0 ? contentElement.ActualHeight : availableHeight);
			}

			double scaleX = availableWidth / designWidth;
			double scaleY = availableHeight / designHeight;
			double scale = Math.Min(scaleX, scaleY);
			scale = Math.Max(minScale, Math.Min(maxScale, scale));

			// Create or reuse a ScaleTransform on the root element's LayoutTransform
			if (rootElement.LayoutTransform is not ScaleTransform scaleTransform)
			{
				scaleTransform = new ScaleTransform(1.0, 1.0);
				rootElement.LayoutTransform = scaleTransform;
			}

			scaleTransform.ScaleX = scale;
			scaleTransform.ScaleY = scale;
		}
	}
}


