// SPDX-License-Identifier: GPL-3.0-only
// Crop geometry follows NTSCRT's MIT-licensed Crop (Finn McKenty, 2026).
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Ntscrt.Windows;

internal sealed record SourceCrop(string Ratio, double X = 0.5, double Y = 0.5, double Zoom = 1)
{
    public const double MaxZoom = 8;
    public static readonly string[] Ratios =
    [
        "1:1", "4:5", "3:4", "5:7", "2:3", "9:16", "1:2",
        "5:4", "4:3", "7:5", "3:2", "16:10", "16:9", "1.85:1", "2:1", "2.39:1"
    ];

    private double Aspect()
    {
        var parts = Ratio?.Split(':', 2);
        if (parts is not [var a, var b] ||
            !decimal.TryParse(a.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var w) || w <= 0 ||
            !decimal.TryParse(b.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var h) || h <= 0)
            throw new InvalidDataException(Localization.T("Ungültiger Quellbeschnitt."));
        return (double)w / (double)h;
    }

    public void Validate()
    {
        _ = Aspect();
        if (!double.IsFinite(X) || X is < 0 or > 1 || !double.IsFinite(Y) || Y is < 0 or > 1 ||
            !double.IsFinite(Zoom) || Zoom is < 1 or > MaxZoom)
            throw new InvalidDataException(Localization.T("Ungültiger Quellbeschnitt."));
    }

    public Int32Rect Rectangle(int width, int height)
    {
        static int Round(double n) => (int)Math.Round(n, MidpointRounding.AwayFromZero);
        static int Even(double n, int max) => Math.Clamp(2 * Round(Math.Min(n, max) / 2), 1, max);
        var aspect = Aspect();
        var sourceAspect = (double)width / height;
        var w = width;
        var h = height;
        if (Math.Abs(sourceAspect / aspect - 1) >= 0.005)
        {
            if (sourceAspect > aspect) w = Even(height * aspect, width);
            else h = Even(width / aspect, height);
        }
        if (Zoom >= 1.0001)
        {
            w = Even(w / Zoom, w);
            h = Even(h / Zoom, h);
        }
        return new(Round((width - w) * X), Round((height - h) * Y), w, h);
    }

    public PixelFrame Apply(PixelFrame frame)
    {
        var rect = Rectangle(frame.Width, frame.Height);
        if (rect.Width == frame.Width && rect.Height == frame.Height) return frame;
        var row = checked(rect.Width * 4);
        var pixels = new byte[checked(row * rect.Height)];
        for (var y = 0; y < rect.Height; y++)
            Buffer.BlockCopy(frame.Pixels, ((rect.Y + y) * frame.Width + rect.X) * 4, pixels, y * row, row);
        return new(rect.Width, rect.Height, pixels);
    }

    public static SourceCrop FromNtscrt(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("ratio", out var ratio) ||
            ratio.ValueKind != JsonValueKind.String)
            throw new InvalidDataException(Localization.T("Ungültiger Quellbeschnitt."));
        double Read(string key, double fallback) => node.TryGetProperty(key, out var value) && value.TryGetDouble(out var number)
            ? number : fallback;
        var position = Read("position", 0.5);
        var crop = new SourceCrop(ratio.GetString()!, Math.Clamp(Read("x", position), 0, 1),
            Math.Clamp(Read("y", position), 0, 1), Math.Clamp(Read("scale", 1), 1, MaxZoom));
        crop.Validate();
        return crop;
    }
}
