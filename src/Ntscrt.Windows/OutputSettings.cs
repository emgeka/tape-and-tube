// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json.Serialization;

namespace Ntscrt.Windows;

[JsonConverter(typeof(JsonStringEnumConverter<OutputFit>))]
internal enum OutputFit { Fit, Crop }

internal sealed record OutputSettings(int Width, int Height, OutputFit Fit = OutputFit.Fit)
{
    public void Validate()
    {
        if (Width < 1 || Height < 1 || Width > 8192 || Height > 8192 || (long)Width * Height > 24_000_000)
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
        return new(Width, Height, scaledWidth, scaledHeight,
            (int)Math.Floor((Width - scaledWidth) / 2.0), (int)Math.Floor((Height - scaledHeight) / 2.0));
    }
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
