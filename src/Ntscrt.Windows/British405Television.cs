// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

internal static class British405Television
{
    private const int ActiveLines = 377;
    private const int HorizontalSamples = 300;
    private static readonly SourceCrop ScreenCrop = new("5:4");

    internal static RenderResult Render(PixelFrame source, SourceCrop? crop, OutputSettings? output, CancellationToken token)
    {
        var cropped = crop?.Apply(source) ?? source;
        var canvas = output ?? new OutputSettings(cropped.Width, cropped.Height);
        var layout = (canvas with { Fit = OutputFit.Fit }).Geometry(5, 4);
        var image = OutputComposer.Compose(ScreenCrop.Apply(cropped),
            new(HorizontalSamples, ActiveLines, HorizontalSamples, ActiveLines, 0, 0));
        var luma = new byte[HorizontalSamples * ActiveLines];
        for (var i = 0; i < luma.Length; i++)
            luma[i] = (byte)((19 * image.Pixels[i * 4] + 183 * image.Pixels[i * 4 + 1] + 54 * image.Pixels[i * 4 + 2]) >> 8);

        // The limited video bandwidth softens horizontal detail before the phosphor response.
        for (var y = 0; y < ActiveLines; y++)
        {
            var row = y * HorizontalSamples;
            for (var x = 0; x < HorizontalSamples; x++)
            {
                var left = row + Math.Max(0, x - 1);
                var right = row + Math.Min(HorizontalSamples - 1, x + 1);
                var level = (7 + (luma[left] + 4 * luma[row + x] + luma[right]) * (225.0 / (6 * 255)));
                var pixel = (row + x) * 4;
                image.Pixels[pixel] = image.Pixels[pixel + 1] = image.Pixels[pixel + 2] = (byte)level;
            }
        }
        token.ThrowIfCancellationRequested();
        var result = OutputComposer.Compose(image, layout);
        var radius = Math.Min(layout.ContentWidth, layout.ContentHeight) * 0.075;
        var scanStrength = 0.23 * Math.Clamp((layout.ContentHeight / (double)ActiveLines - 1.3) / 1.2, 0, 1);
        var horizontalFalloff = new double[layout.ContentWidth];
        for (var x = 0; x < horizontalFalloff.Length; x++)
        {
            var position = (x + 0.5) * 2 / horizontalFalloff.Length - 1;
            horizontalFalloff[x] = 1 - 0.18 * position * position;
        }
        Parallel.For(0, layout.ContentHeight, new ParallelOptions { CancellationToken = token,
            MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, y =>
        {
            var center = (y + 0.5) * 2 / layout.ContentHeight - 1;
            var line = (y + 0.5) * ActiveLines / layout.ContentHeight;
            var scan = 1 - scanStrength * (0.5 + 0.5 * Math.Cos(2 * Math.PI * line));
            var gain = scan * (1 - 0.16 * center * center);
            var cornerY = Math.Max(0, radius - Math.Min(y + 0.5, layout.ContentHeight - y - 0.5));
            var inset = radius - Math.Sqrt(radius * radius - cornerY * cornerY);
            var row = ((y + layout.Top) * layout.Width + layout.Left) * 4;
            for (var x = 0; x < layout.ContentWidth; x++)
            {
                var edge = Math.Min(x + 0.5, layout.ContentWidth - x - 0.5);
                var mask = Math.Clamp(edge - inset + 0.5, 0, 1);
                var pixel = row + x * 4;
                var level = (byte)(result.Pixels[pixel] * gain * horizontalFalloff[x] * mask);
                result.Pixels[pixel] = result.Pixels[pixel + 1] = result.Pixels[pixel + 2] = level;
            }
        });
        return new(result, []);
    }
}
