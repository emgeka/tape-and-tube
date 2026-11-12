// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Ntscrt.Windows;

public partial class MainWindow
{
    private OutputSettings? _previewOutputSettings;
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
        Preview.Source = (ShowOriginal.IsChecked == true ? _source : _output ?? _source)?.Bitmap();
        OriginalPreview.Source = comparing ? _source!.Bitmap() : null;
        OriginalLayer.Visibility = DividerLayer.Visibility = ComparisonControls.Visibility = comparing ? Visibility.Visible : Visibility.Collapsed;
        UpdateComparisonLayout();
    }
    private ComparisonLayout CurrentComparisonLayout() => ComparisonLayout.Calculate(PreviewHost.ActualWidth, PreviewHost.ActualHeight,
        _source!.Width, _source.Height, _output!.Width, _output.Height, _previewOutputSettings?.Fit ?? OutputFit.Fit, ComparePosition.Value / 100);
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
        Canvas.SetLeft(CompareDivider, layout.Divider - CompareDivider.Width / 2); Canvas.SetTop(CompareDivider, view.Top);
        CompareDivider.Height = view.Height;
    }
    private void PreviewSizeChanged(object sender, SizeChangedEventArgs e) => UpdateComparisonLayout();
    private void SplitPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdateComparisonLayout();
    private void CompareDividerDragged(object sender, DragDeltaEventArgs e)
    {
        if (_source == null || _output == null) return;
        var width = CurrentComparisonLayout().Viewport.Width;
        if (width > 0) ComparePosition.Value = Math.Clamp(ComparePosition.Value + e.HorizontalChange / width * 100, 0, 100);
    }
}
