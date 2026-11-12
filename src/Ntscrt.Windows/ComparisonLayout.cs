// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;

namespace Ntscrt.Windows;

// The original follows the output's fit/crop geometry, so the divider does not shift the image.
internal sealed record ComparisonLayout(Rect Viewport, Rect Original, double Divider)
{
    public static ComparisonLayout Calculate(double availableWidth, double availableHeight, int sourceWidth, int sourceHeight,
        int outputWidth, int outputHeight, OutputFit fit, double position)
    {
        if (!double.IsFinite(availableWidth) || !double.IsFinite(availableHeight) || availableWidth <= 0 || availableHeight <= 0
            || sourceWidth <= 0 || sourceHeight <= 0 || outputWidth <= 0 || outputHeight <= 0 || !double.IsFinite(position))
            return new(new Rect(), new Rect(), 0);
        var scale = Math.Min(availableWidth / outputWidth, availableHeight / outputHeight);
        var viewport = new Rect((availableWidth - outputWidth * scale) / 2, (availableHeight - outputHeight * scale) / 2,
            outputWidth * scale, outputHeight * scale);
        var content = new OutputSettings(outputWidth, outputHeight, fit).Geometry(sourceWidth, sourceHeight);
        return new(viewport, new Rect(viewport.Left + content.Left * scale, viewport.Top + content.Top * scale,
            content.ContentWidth * scale, content.ContentHeight * scale), viewport.Left + viewport.Width * Math.Clamp(position, 0, 1));
    }
}
