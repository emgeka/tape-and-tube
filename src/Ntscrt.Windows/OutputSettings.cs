// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Serialization;

namespace Ntscrt.Windows;

[JsonConverter(typeof(JsonStringEnumConverter<OutputFit>))]
internal enum OutputFit { Fit, Crop }

internal sealed record OutputSettings(int Width, int Height, OutputFit Fit = OutputFit.Fit)
{
    internal const int MaxSide = 8192;
    private const int MaxPixels = 24_000_000;

    public void Validate()
    {
        if (!SizeSupported(Width, Height, MaxSide))
            throw new ArgumentException(Localization.T("Breite und Höhe: 1–8192 Pixel, zusammen höchstens 24 Megapixel."));
        if (!Enum.IsDefined(Fit)) throw new ArgumentException(Localization.T("Unbekannte Bildanpassung."));
    }

    public OutputGeometry Geometry(int sourceWidth, int sourceHeight)
    {
        Validate();
        var factor = Fit == OutputFit.Fit ? Math.Min((double)Width / sourceWidth, (double)Height / sourceHeight)
            : Math.Max((double)Width / sourceWidth, (double)Height / sourceHeight);
        var scaledWidth = Math.Max(1, checked((int)Math.Round(sourceWidth * factor)));
        var scaledHeight = Math.Max(1, checked((int)Math.Round(sourceHeight * factor)));
        return Center(Width, Height, scaledWidth, scaledHeight);
    }

    public OutputGeometry ScanlineGeometry(int sourceWidth, int sourceHeight, int lines, int maxSide)
    {
        var requested = Geometry(sourceWidth, sourceHeight);
        if (requested.ContentHeight % lines == 0 && SizeSupported(Width, Height, maxSide)
            && SizeSupported(requested.ContentWidth, requested.ContentHeight, MaxSide)) return requested;
        OutputGeometry? best = null;
        var bestDistance = int.MaxValue;
        // Compare the height of the picture itself. A letterbox or crop can have a different canvas height.
        for (var height = lines; height <= MaxSide; height += lines)
        {
            var distance = Math.Abs(height - requested.ContentHeight);
            if (distance > bestDistance) continue;
            var factor = height / (double)requested.ContentHeight;
            var pictureWidth = Math.Max(1, (int)Math.Round(height * (double)sourceWidth / sourceHeight));
            var width = Math.Max(1, (int)Math.Round(Width * factor));
            var canvasHeight = Math.Max(1, (int)Math.Round(Height * factor));
            if (Fit == OutputFit.Fit) { width = Math.Max(width, pictureWidth); canvasHeight = Math.Max(canvasHeight, height); }
            else { width = Math.Min(width, pictureWidth); canvasHeight = Math.Min(canvasHeight, height); }
            if (!SizeSupported(width, canvasHeight, maxSide) || !SizeSupported(pictureWidth, height, MaxSide)) continue;
            best = Center(width, canvasHeight, pictureWidth, height);
            bestDistance = distance;
        }
        return best ?? requested;
    }

    private static bool SizeSupported(int width, int height, int maxSide) =>
        width >= 1 && height >= 1 && width <= maxSide && height <= maxSide && (long)width * height <= MaxPixels;

    private static OutputGeometry Center(int width, int height, int contentWidth, int contentHeight) =>
        new(width, height, contentWidth, contentHeight,
            (int)Math.Floor((width - contentWidth) / 2.0), (int)Math.Floor((height - contentHeight) / 2.0));
}

internal sealed record OutputGeometry(int Width, int Height, int ContentWidth, int ContentHeight, int Left, int Top)
{
    public (int Width, int Height) CrtRenderSize()
    {
        // Very wide/tall crops need not allocate the entire offscreen picture.
        var limit = Math.Min(1.0, Math.Min(8192.0 / ContentWidth, 8192.0 / ContentHeight));
        limit = Math.Min(limit, Math.Sqrt(24_000_000.0 / ((double)ContentWidth * ContentHeight)));
        return (Math.Max(1, (int)Math.Floor(ContentWidth * limit)), Math.Max(1, (int)Math.Floor(ContentHeight * limit)));
    }
}
