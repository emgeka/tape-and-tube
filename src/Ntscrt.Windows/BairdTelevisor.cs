// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

internal static class BairdTelevisor
{
    private const int Columns = 30;
    private const int VerticalSamples = 70;
    private static readonly SourceCrop ScreenCrop = new("3:7");

    internal static RenderResult Render(PixelFrame source, SourceCrop? crop, OutputSettings? output, int sampleIndex,
        double? sourceRate, CancellationToken token)
    {
        var cropped = crop?.Apply(source) ?? source;
        var canvas = output ?? new OutputSettings(cropped.Width, cropped.Height);
        // The historical aperture keeps its aspect ratio for either output fit mode.
        var layout = (canvas with { Fit = OutputFit.Fit }).Geometry(Columns, VerticalSamples);
        var samples = OutputComposer.Compose(ScreenCrop.Apply(cropped),
            new(Columns, VerticalSamples, Columns, VerticalSamples, 0, 0));
        var pixels = new byte[checked(layout.Width * layout.Height * 4)];
        var columns = new int[layout.ContentWidth];
        var gains = new double[columns.Length];
        var phase = sourceRate is double rate ? sampleIndex * 2 * Math.PI * 2.3 / rate : 0;
        var flicker = 0.95 + 0.05 * Math.Cos(phase);
        for (var x = 0; x < columns.Length; x++)
        {
            var position = (x + 0.5) * Columns / columns.Length;
            columns[x] = (int)position;
            var scan = columns.Length >= Columns * 2 ? Math.Sin(Math.PI * (position - columns[x])) : 1;
            gains[x] = flicker * (0.34 + 0.66 * scan);
        }
        Parallel.For(0, layout.Height, new ParallelOptions { CancellationToken = token,
            MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, y =>
        {
            var row = y * layout.Width * 4;
            for (var x = 0; x < layout.Width; x++) pixels[row + x * 4 + 3] = 255;
            if (y < layout.Top || y >= layout.Top + layout.ContentHeight) return;
            var position = (y - layout.Top + 0.5) * VerticalSamples / layout.ContentHeight - 0.5;
            position = Math.Clamp(position, 0, VerticalSamples - 1);
            var upper = (int)position;
            var lower = Math.Min(upper + 1, VerticalSamples - 1);
            var blend = position - upper;
            for (var x = 0; x < columns.Length; x++)
            {
                var a = Luma((upper * Columns + columns[x]) * 4);
                var b = Luma((lower * Columns + columns[x]) * 4);
                var level = (5 + 0.95 * (a + (b - a) * blend)) * gains[x];
                var target = row + (layout.Left + x) * 4;
                pixels[target] = (byte)(level * 0.14);
                pixels[target + 1] = (byte)(level * 0.45);
                pixels[target + 2] = (byte)level;
            }
        });
        return new(new(layout.Width, layout.Height, pixels), []);

        double Luma(int offset) =>
            0.0722 * samples.Pixels[offset] + 0.7152 * samples.Pixels[offset + 1] + 0.2126 * samples.Pixels[offset + 2];
    }
}
