// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

internal static class KinescopeFilm
{
    private const int FilmWidth = 384;
    private const int FilmHeight = 288;
    private static readonly SourceCrop ScreenCrop = new("4:3");

    internal static RenderResult Render(PixelFrame source, SourceCrop? crop, OutputSettings? output, int sampleIndex,
        double? sourceRate, CancellationToken token, bool preservePicture)
    {
        var cropped = crop?.Apply(source) ?? source;
        var canvas = output ?? new OutputSettings(cropped.Width, cropped.Height);
        var layout = (canvas with { Fit = OutputFit.Fit }).Geometry(4, 3);
        var picture = preservePicture ? cropped : ScreenCrop.Apply(cropped);
        var monitor = OutputComposer.Compose(picture, new OutputSettings(FilmWidth, FilmHeight).Geometry(picture.Width, picture.Height));
        if (ReferenceEquals(monitor, picture)) monitor = monitor with { Pixels = (byte[])monitor.Pixels.Clone() };
        token.ThrowIfCancellationRequested();
        var luma = new byte[FilmWidth * FilmHeight];
        for (var i = 0; i < luma.Length; i++)
            luma[i] = (byte)((19 * monitor.Pixels[i * 4] + 183 * monitor.Pixels[i * 4 + 1] + 54 * monitor.Pixels[i * 4 + 2]) >> 8);

        // Derive the film position and exposure from the picture time so seeking does not alter the effect.
        var frameSeed = Mix((uint)sampleIndex + 397);
        var shiftX = sourceRate.HasValue ? (int)(frameSeed % 3) - 1 : 0;
        var shiftY = sourceRate.HasValue ? (int)((frameSeed >> 8) % 3) - 1 : 0;
        var exposure = sourceRate.HasValue ? 0.975 + 0.05 * (frameSeed >> 24) / 255 : 1;
        var dustX = 4 + (int)(Mix(frameSeed + 1) % (FilmWidth - 8));
        var dustY = 4 + (int)(Mix(frameSeed + 2) % (FilmHeight - 8));

        Parallel.For(0, FilmHeight, new ParallelOptions { CancellationToken = token,
            MaxDegreeOfParallelism = Math.Min(4, Environment.ProcessorCount) }, y =>
        {
            var monitorY = Math.Clamp(y + shiftY, 0, FilmHeight - 1);
            var row = monitorY * FilmWidth;
            var previous = Math.Max(0, monitorY - 1) * FilmWidth;
            var next = Math.Min(FilmHeight - 1, monitorY + 1) * FilmWidth;
            var vertical = (2.0 * (y + 0.5) / FilmHeight - 1);
            for (var x = 0; x < FilmWidth; x++)
            {
                var monitorX = Math.Clamp(x + shiftX, 0, FilmWidth - 1);
                var left = Math.Max(0, monitorX - 1);
                var right = Math.Min(FilmWidth - 1, monitorX + 1);
                var signal = (luma[row + monitorX] * 4 + luma[row + left] + luma[row + right]
                    + luma[previous + monitorX] + luma[next + monitorX]) / (8.0 * 255);

                var horizontal = 2.0 * (x + 0.5) / FilmWidth - 1;
                var falloff = 1 - 0.14 * (horizontal * horizontal + vertical * vertical);
                var grain = (int)(Mix(frameSeed ^ (uint)(y * FilmWidth + x)) >> 28) - 7;
                // The film print lifts the blacks and rolls off tube highlights.
                var level = (17 + 220 * signal - 23 * signal * signal) * exposure * falloff + grain;
                if (frameSeed % 4 == 0 && Math.Abs(y - dustY) + Math.Abs(x - dustX) < 2) level *= 0.56;
                var pixel = (y * FilmWidth + x) * 4;
                monitor.Pixels[pixel] = monitor.Pixels[pixel + 1] = monitor.Pixels[pixel + 2]
                    = (byte)Math.Clamp(level, 0, 255);
            }
        });
        return new(OutputComposer.Compose(monitor, layout), []);
    }

    private static uint Mix(uint value)
    {
        value = unchecked((value ^ (value >> 16)) * 0x7feb352dU);
        value = unchecked((value ^ (value >> 15)) * 0x846ca68bU);
        return value ^ (value >> 16);
    }
}
