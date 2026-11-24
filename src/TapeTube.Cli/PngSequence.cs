// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal sealed record PngPosition(string Path, double RequestedSeconds, int FrameIndex, double ActualSeconds);

internal static class PngSequence
{
    internal const int MaxPositions = 256;

    internal static double[] Parse(string value)
    {
        var parts = value.Split(',', MaxPositions + 1);
        if (parts.Length > MaxPositions) throw new ArgumentException($"Supply at most {MaxPositions} PNG times.");
        return parts.Select(part =>
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                !double.IsFinite(seconds) || seconds < 0)
                throw new ArgumentException("--time requires comma-separated non-negative seconds, for example 0,1.5,3.");
            return seconds;
        }).ToArray();
    }

    internal static PngPosition[] Positions(string output, VideoInfo info, double[] times)
    {
        var directory = Path.GetDirectoryName(output)!;
        var stem = Path.GetFileNameWithoutExtension(output);
        return times.Select((seconds, i) =>
        {
            var index = (int)Math.Min(info.FrameCount - 1, Math.Round(seconds * info.FramesPerSecond, MidpointRounding.AwayFromZero));
            var path = times.Length == 1 ? output
                : Path.Combine(directory, FormattableString.Invariant($"{stem}-{i + 1:D3}-frame-{index:D6}.png"));
            return new PngPosition(path, seconds, index, index / info.FramesPerSecond);
        }).ToArray();
    }

    internal static async Task<object> ExportAsync(string source, PngPosition[] positions, VideoInfo info,
        RenderSettings settings, Func<int, CancellationToken, Task<PixelFrame>> readFrame, bool overwrite,
        PngCloseup? closeup, CancellationToken token)
    {
        foreach (var position in positions) VideoExporter.CheckTarget(source, position.Path, overwrite);

        var staged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var width = 0;
        var height = 0;
        using var pipeline = new EffectPipeline();
        try
        {
            // A single decoder and renderer run forwards, preserving temporal history.
            // Publish only once every frame has rendered and all PNGs have been encoded.
            foreach (var group in positions.GroupBy(p => p.FrameIndex).OrderBy(group => group.Key))
            {
                token.ThrowIfCancellationRequested();
                var image = await readFrame(group.Key, token).ConfigureAwait(false);
                var result = await pipeline.RenderAsync(image, settings with { Frame = (uint)group.Key }, "command-line",
                    info, info.IsStill ? null : readFrame, token).ConfigureAwait(false);
                var png = closeup?.Apply(result.Frame, token) ?? result.Frame;
                width = png.Width;
                height = png.Height;
                string? encoded = null;
                foreach (var position in group)
                {
                    token.ThrowIfCancellationRequested();
                    var temporary = Path.Combine(Path.GetDirectoryName(position.Path)!, $".tapetube-{Guid.NewGuid():N}.png");
                    staged.Add(position.Path, temporary);
                    if (encoded == null)
                    {
                        png.Save(temporary);
                        encoded = temporary;
                    }
                    else File.Copy(encoded, temporary);
                }
            }
            foreach (var position in positions) VideoExporter.CheckTarget(source, position.Path, overwrite);
            token.ThrowIfCancellationRequested();
            var published = 0;
            try
            {
                foreach (var position in positions)
                {
                    File.Move(staged[position.Path], position.Path, overwrite);
                    published++;
                }
            }
            catch (Exception ex) when (published > 0 && ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Wrote {published} of {positions.Length} PNG files. Last written: {positions[published - 1].Path}. {ex.Message}", ex);
            }
            return new { format = "png", frames = positions.Length, width, height, outputs = positions };
        }
        finally
        {
            foreach (var temporary in staged.Values)
                if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
