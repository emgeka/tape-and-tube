// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

/// Aspect-preserving output composition. Upscaling uses bilinear filtering;
/// downscaling integrates pixel area to avoid aliasing fine scanline patterns.
internal static class OutputComposer
{
    private readonly record struct Tap(int Index, double Weight);
    public static PixelFrame Compose(PixelFrame input, OutputGeometry layout)
    {
        if (input.Width == layout.Width && input.Height == layout.Height && layout.Left == 0 && layout.Top == 0
            && layout.ContentWidth == input.Width && layout.ContentHeight == input.Height) return input;
        var bytes = new byte[checked(layout.Width * layout.Height * 4)];
        for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
        var left = Math.Max(0, layout.Left); var top = Math.Max(0, layout.Top);
        var right = Math.Min(layout.Width, layout.Left + layout.ContentWidth);
        var bottom = Math.Min(layout.Height, layout.Top + layout.ContentHeight);
        if (input.Width == layout.ContentWidth && input.Height == layout.ContentHeight)
        {
            for (int y = top; y < bottom; y++)
                Buffer.BlockCopy(input.Pixels, ((y - layout.Top) * input.Width + left - layout.Left) * 4,
                    bytes, (y * layout.Width + left) * 4, (right - left) * 4);
            return new(layout.Width, layout.Height, bytes);
        }
        var xs = Axis(input.Width, layout.ContentWidth, left - layout.Left, right - left);
        var ys = Axis(input.Height, layout.ContentHeight, top - layout.Top, bottom - top);
        Parallel.For(top, bottom, new ParallelOptions { MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, y =>
        {
            var rows = ys[y - top];
            for (int x = left; x < right; x++)
            {
                double b = 0, g = 0, r = 0;
                foreach (var row in rows) foreach (var column in xs[x - left])
                {
                    var at = (row.Index * input.Width + column.Index) * 4;
                    var weight = row.Weight * column.Weight;
                    b += input.Pixels[at] * weight; g += input.Pixels[at + 1] * weight; r += input.Pixels[at + 2] * weight;
                }
                var target = (y * layout.Width + x) * 4;
                bytes[target] = (byte)Math.Clamp(Math.Round(b), 0, 255);
                bytes[target + 1] = (byte)Math.Clamp(Math.Round(g), 0, 255);
                bytes[target + 2] = (byte)Math.Clamp(Math.Round(r), 0, 255);
            }
        });
        return new(layout.Width, layout.Height, bytes);
    }

    private static Tap[][] Axis(int source, int destination, int offset, int count)
    {
        var result = new Tap[count][];
        var ratio = (double)source / destination;
        for (int i = 0; i < count; i++)
        {
            var position = i + offset;
            if (ratio > 1)
            {
                var from = position * ratio; var to = (position + 1) * ratio;
                var taps = new List<Tap>();
                for (int index = Math.Max(0, (int)Math.Floor(from)); index < Math.Min(source, (int)Math.Ceiling(to)); index++)
                    taps.Add(new(index, Math.Max(0, Math.Min(to, index + 1) - Math.Max(from, index)) / ratio));
                result[i] = taps.ToArray();
            }
            else
            {
                var center = Math.Clamp((position + 0.5) * ratio - 0.5, 0, source - 1);
                var lower = (int)Math.Floor(center); var upper = Math.Min(source - 1, lower + 1);
                var fraction = center - lower;
                result[i] = lower == upper ? [new(lower, 1)] : [new(lower, 1 - fraction), new(upper, fraction)];
            }
        }
        return result;
    }
}
