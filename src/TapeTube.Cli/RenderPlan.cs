// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json.Nodes;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal static class RenderPlan
{
    internal static object Describe(string source, string output, VideoInfo info, RenderSettings settings, int? frameIndex,
        VideoExportFormat videoFormat, Mp4Quality quality, int loops, GifExportSettings gifSettings, bool overwrite,
        PngPosition[]? positions = null, PngCloseup? closeup = null)
    {
        var png = frameIndex.HasValue || positions != null;
        var gif = !png && videoFormat == VideoExportFormat.Gif;
        var mp4 = !png && !gif;
        var timing = gif ? new VideoExporter.GifTiming(info, gifSettings.Fps) : null;
        if (gif) settings = VideoExporter.ConfigureGifOutput(info.Width, info.Height, settings, gifSettings);
        var geometry = EffectPipeline.PlanOutput(info.Width, info.Height, settings);
        var closeupCrop = closeup?.Crop(geometry.Width, geometry.Height);
        var frames = png ? positions?.Length ?? 1 : timing?.Count ?? checked(info.FrameCount * loops);
        var imported = settings.ImportedLook is { } look ? JsonNode.Parse(look.Json) : null;
        var ntsc = settings.Ntsc ? NtscProcessor.Defaults() : null;
        if (ntsc != null)
            foreach (var (key, value) in JsonNode.Parse(settings.NtscJson)!.AsObject()) ntsc[key] = value?.DeepClone();
        return new
        {
            dryRun = true,
            source,
            path = output,
            targetExists = positions?.Any(p => File.Exists(p.Path)) ?? File.Exists(output),
            targetDirectoryExists = Directory.Exists(Path.GetDirectoryName(output)),
            targetIsSource = positions?.Any(p => string.Equals(source, p.Path, StringComparison.OrdinalIgnoreCase))
                ?? string.Equals(source, output, StringComparison.OrdinalIgnoreCase),
            overwrite,
            sourceWidth = info.Width,
            sourceHeight = info.Height,
            format = png ? "png" : gif ? "gif" : "mp4",
            codec = mp4 ? videoFormat == VideoExportFormat.Hevc ? "hevc" : "h264" : null,
            quality = mp4 ? quality == Mp4Quality.VeryHigh ? "very-high" : quality.ToString().ToLowerInvariant() : null,
            loops = mp4 ? loops : (int?)null,
            width = mp4 ? VideoExporter.Mp4Dimension(geometry.Width) : geometry.Width,
            height = mp4 ? VideoExporter.Mp4Dimension(geometry.Height) : geometry.Height,
            frames,
            duration = png ? 0 : timing?.Duration ?? frames / info.FramesPerSecond,
            fps = png ? (double?)null : timing?.Fps ?? info.FramesPerSecond,
            frameIndex,
            closeup = closeupCrop is { } crop ? new
            {
                closeup!.Zoom, closeup.CenterX, closeup.CenterY,
                crop = new { crop.X, crop.Y, crop.Width, crop.Height }
            } : null,
            outputs = positions?.Select(position => new
            {
                position.Path, position.RequestedSeconds, position.FrameIndex, position.ActualSeconds,
                width = geometry.Width, height = geometry.Height, targetExists = File.Exists(position.Path)
            }),
            renderer = settings.Renderer,
            effects = new
            {
                historicalDisplay = settings.Display == HistoricalDisplay.None ? (HistoricalDisplay?)null : settings.Display,
                kinescopeFilm = settings.Film,
                snapScanlines = settings.SnapScanlines,
                scanlineRows = geometry.ScanlineRows,
                sourceCrop = settings.Crop,
                outputFit = settings.Output?.Fit,
                downscaleEnabled = EffectPipeline.UsesDownscale(settings),
                retroWidth = settings.RetroWidth,
                downscaleMethod = imported?["downscale"]?["method"]?.ToString()
                    ?? settings.DownscaleMethod ?? (imported != null ? "nearest" : settings.Renderer == "legacy-v1" ? "wic-fant" : "area"),
                ntsc,
                crt = settings.Crt && imported == null
                    ? new { shader = Path.GetFileNameWithoutExtension(settings.Shader), values = settings.ShaderValues } : null,
                digital = settings.ScreenLoop == null ? settings.Digital : null,
                analog = settings.Analog,
                screenLoop = settings.ScreenLoop,
                timeline = settings.EffectTimeline,
                importedLook = imported
            }
        };
    }
}
