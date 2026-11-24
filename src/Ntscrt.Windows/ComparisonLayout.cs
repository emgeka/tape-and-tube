// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;

namespace Ntscrt.Windows;

internal readonly record struct PreviewGrid(double LineHeight, int ContentTop, DpiScale Dpi);

// The original follows the output's fit/crop geometry, so the divider does not shift the image.
internal sealed record ComparisonLayout(Rect Viewport, Rect Original, double Divider)
{
    public static ComparisonLayout Calculate(double availableWidth, double availableHeight, int sourceWidth, int sourceHeight,
        int outputWidth, int outputHeight, OutputFit fit, double position, OutputGeometry? layout = null,
        double zoom = 1, Vector pan = default, PreviewGrid? grid = null)
    {
        if (!double.IsFinite(availableWidth) || !double.IsFinite(availableHeight) || availableWidth <= 0 || availableHeight <= 0
            || sourceWidth <= 0 || sourceHeight <= 0 || outputWidth <= 0 || outputHeight <= 0 || !double.IsFinite(position))
            return new(new Rect(), new Rect(), 0);
        var viewport = ImageBounds(new Size(availableWidth, availableHeight), outputWidth, outputHeight, zoom, pan, grid);
        var scale = viewport.Width / outputWidth;
        var content = layout ?? new OutputSettings(outputWidth, outputHeight, fit).Geometry(sourceWidth, sourceHeight);
        return new(viewport, new Rect(viewport.Left + content.Left * scale, viewport.Top + content.Top * scale,
            content.ContentWidth * scale, content.ContentHeight * scale), viewport.Left + viewport.Width * Math.Clamp(position, 0, 1));
    }

    public static Rect ImageBounds(Size available, int width, int height, double zoom, Vector pan, PreviewGrid? grid = null)
    {
        var scale = zoom * Math.Min(available.Width / width, available.Height / height);
        var snap = false;
        if (grid is { } sampling)
        {
            var physicalLineHeight = sampling.LineHeight * sampling.Dpi.DpiScaleY;
            var rowsPerLine = Math.Floor(scale * physicalLineHeight + 1e-9);
            if (rowsPerLine >= 1)
            {
                scale = rowsPerLine / physicalLineHeight;
                snap = true;
            }
        }
        var w = width * scale;
        var h = height * scale;
        var centerX = (available.Width - w) / 2;
        var centerY = (available.Height - h) / 2;
        var limitX = Math.Max(0, -centerX);
        var limitY = Math.Max(0, -centerY);
        var left = centerX + Math.Clamp(pan.X, -limitX, limitX);
        var top = centerY + Math.Clamp(pan.Y, -limitY, limitY);
        if (snap && grid is { } active)
        {
            left = AlignOrigin(left, available.Width, w, 0, active.Dpi.DpiScaleX);
            top = AlignOrigin(top, available.Height, h, active.ContentTop * scale, active.Dpi.DpiScaleY);
        }
        return new(left, top, w, h);
    }

    private static double AlignOrigin(double origin, double available, double length, double inset, double dpi)
    {
        var pixel = Math.Round((origin + inset) * dpi);
        if (length > available)
        {
            // Keep an overflowing image covering the viewport while panning.
            var first = Math.Ceiling((available - length + inset) * dpi);
            var last = Math.Floor(inset * dpi);
            if (first > last) return origin;
            pixel = Math.Clamp(pixel, first, last);
        }
        return pixel / dpi - inset;
    }

    public static Vector PanAtZoomAnchor(Size available, Rect current, Point anchor, double factor)
    {
        var imageCenter = new Point(current.Left + current.Width / 2, current.Top + current.Height / 2);
        var hostCenter = new Point(available.Width / 2, available.Height / 2);
        return (imageCenter - anchor) * factor + (anchor - hostCenter);
    }
}
