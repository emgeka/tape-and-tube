// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal sealed record PngCloseup(double Zoom, double CenterX, double CenterY)
{
    internal const int MaxZoom = 12;

    internal static PngCloseup? Parse(string? zoom, string? center, bool png)
    {
        if (zoom == null && center == null) return null;
        if (!png || zoom == null) throw new ArgumentException("PNG close-ups require --zoom. Use --zoom-at to choose the center.");
        if (!ReadNumber(zoom, out var factor) || factor is < 1 or > MaxZoom)
            throw new ArgumentException($"--zoom must be between 1 and {MaxZoom}.");
        if (center == null) return new(factor, 0.5, 0.5);
        if (center.Split(',') is not [var x, var y] || !ReadNumber(x, out var cx) || !ReadNumber(y, out var cy) ||
            cx is < 0 or > 1 || cy is < 0 or > 1)
            throw new ArgumentException("--zoom-at requires x,y coordinates between 0 and 1.");
        return new(factor, cx, cy);
    }

    private static bool ReadNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    internal Int32Rect Crop(int width, int height)
    {
        var w = Math.Max(1, (int)Math.Round(width / Zoom, MidpointRounding.AwayFromZero));
        var h = Math.Max(1, (int)Math.Round(height / Zoom, MidpointRounding.AwayFromZero));
        var left = Math.Clamp((int)(CenterX * width) - w / 2, 0, width - w);
        var top = Math.Clamp((int)(CenterY * height) - h / 2, 0, height - h);
        return new(left, top, w, h);
    }

    internal PixelFrame Apply(PixelFrame frame, CancellationToken token)
    {
        var crop = Crop(frame.Width, frame.Height);
        if (crop.Width == frame.Width && crop.Height == frame.Height) return frame;

        var pixels = new byte[frame.Pixels.Length];
        Parallel.For(0, frame.Height, new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount)
        }, y =>
        {
            var sourceY = crop.Y + Sample(y, crop.Height, frame.Height);
            var source = MemoryMarshal.Cast<byte, uint>(frame.Pixels.AsSpan()).Slice(sourceY * frame.Width + crop.X, crop.Width);
            var destination = MemoryMarshal.Cast<byte, uint>(pixels.AsSpan()).Slice(y * frame.Width, frame.Width);
            for (var x = 0; x < destination.Length; x++) destination[x] = source[Sample(x, crop.Width, frame.Width)];
        });
        return new(frame.Width, frame.Height, pixels);
    }

    private static int Sample(int index, int size, int targetSize) =>
        (int)((2L * index + 1) * size / (2L * targetSize));
}
