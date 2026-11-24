// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private Vector _previewPan;
    private Point? _panPointer, _zoomAnchor;
    private int _previewScanlineRows;

    private PixelFrame? DisplayedPreviewFrame => ShowOriginal.IsChecked == true ? _source : _output ?? _source;
    private Size PreviewSize => new(PreviewHost.ActualWidth, PreviewHost.ActualHeight);

    private PreviewGrid? CurrentPreviewGrid(PixelFrame frame)
    {
        if (IntegerPreviewChoice.IsChecked != true) return null;
        var useOutput = ShowOriginal.IsChecked != true && _output != null;
        var layout = useOutput ? _previewLayout : null;
        var rows = useOutput && _previewScanlineRows > 0 ? _previewScanlineRows : frame.Height;
        return new((layout?.ContentHeight ?? frame.Height) / (double)rows, layout?.Top ?? 0, VisualTreeHelper.GetDpi(PreviewHost));
    }

    private void UpdatePreviewNavigationLayout()
    {
        if (!_ready || DisplayedPreviewFrame is not { } frame) return;
        var size = PreviewSize;
        var grid = CurrentPreviewGrid(frame);
        var view = ComparisonLayout.ImageBounds(size, frame.Width, frame.Height, PreviewZoomChoice.Value, _previewPan, grid);
        _previewPan = new(view.Left + (view.Width - size.Width) / 2, view.Top + (view.Height - size.Height) / 2);
        Canvas.SetLeft(Preview, view.Left);
        Canvas.SetTop(Preview, view.Top);
        Preview.Width = view.Width;
        Preview.Height = view.Height;
        SetPreviewSampling(Preview, view, grid);
        UpdateComparisonLayout();
        UpdateScreenLoopPad();
    }

    private static void SetPreviewSampling(Image image, Rect view, PreviewGrid? grid)
    {
        // Keep area filtering when shrinking a CRT render. Use crisp pixels for exact enlargement of a bitmap.
        var mode = BitmapScalingMode.HighQuality;
        if (grid is { } sampling && image.Source is BitmapSource bitmap)
        {
            var x = view.Width * sampling.Dpi.DpiScaleX / bitmap.PixelWidth;
            var y = view.Height * sampling.Dpi.DpiScaleY / bitmap.PixelHeight;
            if (x >= 1 && y >= 1 && Math.Abs(x - Math.Round(x)) < 1e-6 && Math.Abs(y - Math.Round(y)) < 1e-6)
                mode = BitmapScalingMode.NearestNeighbor;
        }
        RenderOptions.SetBitmapScalingMode(image, mode);
    }

    private void IntegerPreviewChanged(object sender, RoutedEventArgs e) => UpdatePreviewNavigationLayout();

    private void PreviewZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready) return;
        if (DisplayedPreviewFrame is { } frame)
        {
            var size = PreviewSize;
            var grid = CurrentPreviewGrid(frame);
            var oldView = ComparisonLayout.ImageBounds(size, frame.Width, frame.Height, e.OldValue, _previewPan, grid);
            var newView = ComparisonLayout.ImageBounds(size, frame.Width, frame.Height, e.NewValue, _previewPan, grid);
            var anchor = _zoomAnchor ?? new Point(size.Width / 2, size.Height / 2);
            if (oldView.Width > 0)
                _previewPan = ComparisonLayout.PanAtZoomAnchor(size, oldView, anchor, newView.Width / oldView.Width);
        }
        FitPreviewButton.IsEnabled = e.NewValue > 1;
        PreviewHost.Cursor = e.NewValue > 1 ? Cursors.SizeAll : null;
        PreviewZoomValue.Text = e.NewValue.ToString("P0", CultureInfo.GetCultureInfo(Localization.Language));
        UpdatePreviewNavigationLayout();
    }

    private void ZoomWithWheel(object sender, MouseWheelEventArgs e)
    {
        if (DisplayedPreviewFrame == null) return;
        _zoomAnchor = e.GetPosition(PreviewHost);
        try
        {
            PreviewZoomChoice.Value = Math.Clamp(PreviewZoomChoice.Value * Math.Pow(1.25, e.Delta / 120.0),
                PreviewZoomChoice.Minimum, PreviewZoomChoice.Maximum);
        }
        finally { _zoomAnchor = null; }
        e.Handled = true;
    }

    private void FitPreview(object sender, RoutedEventArgs e) => ResetPreviewNavigation();

    private void ResetPreviewNavigation()
    {
        if (PreviewHost.IsMouseCaptured) PreviewHost.ReleaseMouseCapture();
        _previewPan = default;
        PreviewZoomChoice.Value = 1;
        UpdatePreviewNavigationLayout();
    }

    private void BeginPreviewPan(object sender, MouseButtonEventArgs e)
    {
        if (DisplayedPreviewFrame == null || PreviewZoomChoice.Value <= 1 || !PreviewHost.CaptureMouse()) return;
        _panPointer = e.GetPosition(PreviewHost);
        e.Handled = true;
    }

    private void DragPreview(object sender, MouseEventArgs e)
    {
        if (_panPointer is not { } previous) return;
        var current = e.GetPosition(PreviewHost);
        _previewPan += current - previous;
        _panPointer = current;
        UpdatePreviewNavigationLayout();
        e.Handled = true;
    }

    private void EndPreviewPan(object sender, MouseButtonEventArgs e)
    {
        if (!PreviewHost.IsMouseCaptured) return;
        PreviewHost.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void PreviewCaptureLost(object sender, MouseEventArgs e) => _panPointer = null;
}
