// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private OutputGeometry? _previewLayout;
    private void CompareChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (ShowOriginal.IsChecked == true) SplitComparison.IsChecked = false;
        UpdatePreview();
    }
    private void SplitComparisonChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        if (SplitComparison.IsChecked == true) ShowOriginal.IsChecked = false;
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        var comparing = SplitComparison.IsChecked == true && _source != null && _output != null;
        SplitComparison.IsEnabled = _source != null && _output != null;
        var frame = DisplayedPreviewFrame;
        Preview.Source = frame?.Bitmap();
        PreviewNavigation.Visibility = frame == null ? Visibility.Collapsed : Visibility.Visible;
        OriginalPreview.Source = comparing
            ? _previewCrop is { } crop
                ? new CroppedBitmap(_source!.Bitmap(), crop.Rectangle(_source.Width, _source.Height))
                : _source!.Bitmap()
            : null;
        OriginalLayer.Visibility = DividerLayer.Visibility = ComparisonControls.Visibility = comparing ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreviewNavigationLayout();
    }
    private ComparisonLayout CurrentComparisonLayout()
    {
        var source = _source!;
        var rect = _previewCrop?.Rectangle(source.Width, source.Height);
        return ComparisonLayout.Calculate(PreviewHost.ActualWidth, PreviewHost.ActualHeight, rect?.Width ?? source.Width,
            rect?.Height ?? source.Height, _output!.Width, _output.Height, OutputFit.Fit, ComparePosition.Value / 100,
            _previewLayout, PreviewZoomChoice.Value, _previewPan, CurrentPreviewGrid(_output));
    }
    private void UpdateComparisonLayout()
    {
        if (PreviewHost == null || _source == null || _output == null || OriginalLayer.Visibility != Visibility.Visible) return;
        var layout = CurrentComparisonLayout();
        var view = layout.Viewport;
        OriginalLayer.Clip = new RectangleGeometry(new Rect(view.Left, view.Top, Math.Max(0, layout.Divider - view.Left), view.Height));
        Canvas.SetLeft(OriginalMatte, view.Left); Canvas.SetTop(OriginalMatte, view.Top);
        OriginalMatte.Width = view.Width; OriginalMatte.Height = view.Height;
        Canvas.SetLeft(OriginalPreview, layout.Original.Left); Canvas.SetTop(OriginalPreview, layout.Original.Top);
        OriginalPreview.Width = layout.Original.Width; OriginalPreview.Height = layout.Original.Height;
        SetPreviewSampling(OriginalPreview, layout.Original, CurrentPreviewGrid(_output));
        var visibleTop = Math.Max(0, view.Top);
        var visibleBottom = Math.Min(PreviewHost.ActualHeight, view.Bottom);
        CompareDivider.Visibility = layout.Divider >= 0 && layout.Divider <= PreviewHost.ActualWidth
            ? Visibility.Visible : Visibility.Collapsed;
        Canvas.SetLeft(CompareDivider, layout.Divider - CompareDivider.Width / 2);
        Canvas.SetTop(CompareDivider, visibleTop);
        CompareDivider.Height = Math.Max(0, visibleBottom - visibleTop);
    }
    private void PreviewSizeChanged(object sender, SizeChangedEventArgs e) => UpdatePreviewNavigationLayout();
    private void SplitPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateComparisonLayout();
    private void CompareDividerDragged(object sender, DragDeltaEventArgs e)
    {
        if (_source == null || _output == null) return;
        var width = CurrentComparisonLayout().Viewport.Width;
        if (width > 0) ComparePosition.Value = Math.Clamp(ComparePosition.Value + e.HorizontalChange / width * 100, 0, 100);
    }
}
