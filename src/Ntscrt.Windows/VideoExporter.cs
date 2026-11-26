// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Ntscrt.Windows;

internal sealed record ExportProgress(int CompletedFrames, int TotalFrames, int Width, int Height, bool Finalizing = false);
internal sealed record ExportResult(string Path, int Frames, int Width, int Height, double Duration);
internal enum VideoExportFormat { Mp4, Gif, Hevc, ProRes, WebM }
internal enum Mp4Quality { Standard, High, VeryHigh, Maximum }
internal sealed record GifExportSettings(int? Width = null, int? Fps = null)
{
    public const int MinWidth = 64, MaxWidth = 1920, MaxFps = 50;
}
internal sealed record GifEstimate(int Width, int Height, int Frames)
{
    public double Megabytes => Width * (double)Height * Frames * 0.8 / 1_000_000;
    public bool IsLarge => Megabytes > 10;
}

internal static class VideoExporter
{
    internal const int MaxLoops = 50;

    internal static string Extension(VideoExportFormat format) => format switch
    {
        VideoExportFormat.Mp4 or VideoExportFormat.Hevc => "mp4",
        VideoExportFormat.Gif => "gif",
        VideoExportFormat.ProRes => "mov",
        VideoExportFormat.WebM => "webm",
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static async Task<ExportResult> ExportAsync(string sourcePath, string outputPath, RenderSettings settings,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellation = default, bool overwrite = false,
        Action<int, PixelFrame>? frameObserver = null, VideoExportFormat format = VideoExportFormat.Mp4,
        Mp4Quality quality = Mp4Quality.High, int loops = 1, GifExportSettings? gifSettings = null, bool sideBySide = false)
    {
        if (loops is < 1 or > MaxLoops || (loops > 1 && format == VideoExportFormat.Gif))
            throw new ArgumentOutOfRangeException(nameof(loops));
        sourcePath = Path.GetFullPath(sourcePath);
        var target = CheckTarget(sourcePath, outputPath, overwrite);
        cancellation.ThrowIfCancellationRequested();
        await using var source = await VideoSource.OpenAsync(sourcePath, cancellation).ConfigureAwait(false);
        return await ExportFramesAsync(source.Info, source.ReadFrameAsync, target, settings,
            progress, cancellation, overwrite, frameObserver, format, quality, loops, gifSettings, sideBySide).ConfigureAwait(false);
    }

    public static Task<ExportResult> ExportStillAsync(PixelFrame source, string? imagePath, string outputPath,
        double seconds, RenderSettings settings, IProgress<ExportProgress>? progress = null,
        CancellationToken cancellation = default, bool overwrite = false, Action<int, PixelFrame>? frameObserver = null,
        int fps = ScreenLoopCatalog.StillFrameRate, VideoExportFormat format = VideoExportFormat.Mp4,
        Mp4Quality quality = Mp4Quality.High, GifExportSettings? gifSettings = null, bool sideBySide = false)
    {
        var maximumSeconds = settings.ScreenLoop == null ? NtscrtImportedLook.MaxStillSeconds : 60;
        if (!double.IsFinite(seconds) || seconds < 0.5 || seconds > maximumSeconds || fps is < 1 or > 60)
            throw new ArgumentException("Invalid still animation settings.");
        var target = CheckTarget(imagePath, outputPath, overwrite);
        return ExportFramesAsync(ScreenLoopCatalog.StillTimeline(source, seconds, fps), (_, _) => Task.FromResult(source),
            target, settings, progress, cancellation, overwrite, frameObserver, format, quality, gifSettings: gifSettings, sideBySide: sideBySide);
    }

    internal static string CheckTarget(string? sourcePath, string outputPath, bool overwrite)
    {
        outputPath = Path.GetFullPath(outputPath);
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization.T("Bitte einen anderen Dateinamen wählen. Das Quellvideo wird nicht überschrieben."));
        if (File.Exists(outputPath) && !overwrite) throw new IOException(Localization.T("Die Zieldatei existiert bereits."));
        var directory = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(Localization.T("Der Zielordner existiert nicht."));
        return outputPath;
    }

    private static async Task<ExportResult> ExportFramesAsync(VideoInfo info,
        Func<int, CancellationToken, Task<PixelFrame>> readFrame, string outputPath, RenderSettings settings,
        IProgress<ExportProgress>? progress, CancellationToken cancellation, bool overwrite, Action<int, PixelFrame>? frameObserver,
        VideoExportFormat format, Mp4Quality quality, int loops = 1, GifExportSettings? gifSettings = null, bool sideBySide = false)
    {
        var totalFrames = checked(info.FrameCount * loops);
        var extension = Extension(format);
        if (format is VideoExportFormat.ProRes or VideoExportFormat.WebM)
            await CheckAdditionalEncodersAsync(info, format, cancellation).ConfigureAwait(false);
        // Freeze caller-owned values, including the parameter dictionary, for the whole job.
        settings = settings with { ShaderValues = new(settings.ShaderValues), Digital=settings.Digital==null?null:new(settings.Digital),
            Analog = settings.Analog?.Snapshot(), ScreenLoop = settings.ScreenLoop == null ? null : new(settings.ScreenLoop) };
        cancellation.ThrowIfCancellationRequested();
        using var pipeline = new EffectPipeline();
        var first = await readFrame(0, cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        var gif = format == VideoExportFormat.Gif ? new GifTiming(info, gifSettings?.Fps) : null;
        if (gif != null && !sideBySide) settings = ConfigureGifOutput(first.Width, first.Height, settings, gifSettings);
        var sourceId=Guid.NewGuid().ToString("N");
        async Task<(PixelFrame Result, PixelFrame? Source)> RenderFrame(int index)
        {
            var current = settings with { Frame = (uint)index };
            if (settings.HasHistoricalStages && !info.IsStill)
                return ((await pipeline.RenderHistoricalVideoAsync(current, sourceId, info, readFrame, cancellation)
                    .ConfigureAwait(false)).Frame, index == 0 ? first : null);
            var decoded = index == 0 ? first : await readFrame(index, cancellation).ConfigureAwait(false);
            return ((await pipeline.RenderAsync(decoded, current, sourceId, info, info.IsStill ? null : readFrame, cancellation)
                .ConfigureAwait(false)).Frame, decoded);
        }
        var firstRendered = await RenderFrame(0).ConfigureAwait(false);
        var comparison = sideBySide
            ? ComparisonOutput(firstRendered.Result.Width, firstRendered.Result.Height, settings.Output, gif != null, gifSettings?.Width)
            : null;
        var inputWidth = comparison?.Width ?? firstRendered.Result.Width;
        var inputHeight = comparison?.Height ?? firstRendered.Result.Height;
        var width = gif == null ? Mp4Dimension(inputWidth) : inputWidth;
        var height = gif == null ? Mp4Dimension(inputHeight) : inputHeight;
        var temporary = Path.Combine(Path.GetDirectoryName(outputPath)!, $".tapetube-{Guid.NewGuid():N}.{extension}");
        var audioCycle = loops > 1 ? Path.ChangeExtension(temporary, ".wav") : null;
        Process? encoder = null;
        Task<string>? errors = null;
        try
        {
            var hasAudio = audioCycle != null && await CreateAudioCycleAsync(info, audioCycle, cancellation).ConfigureAwait(false);
            var encodeArgs = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin", "-y" };
            if (gif == null) encodeArgs.Add("-copyts");
            encodeArgs.AddRange([
                "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{inputWidth}x{inputHeight}",
                "-framerate", gif?.Rate ?? info.Rate, "-i", "pipe:0"]);
            if (gif == null) AddVideoOptions(encodeArgs, info, format, quality, loops, hasAudio ? audioCycle : null);
            else encodeArgs.AddRange(["-an", "-filter_complex_threads", "1", "-filter_complex",
                "[0:v]split[image][colors];[colors]palettegen=stats_mode=single:reserve_transparent=0[palette];[image][palette]paletteuse=new=1:dither=bayer:bayer_scale=3",
                "-fps_mode", "passthrough", "-loop", "0", "-final_delay", gif.FinalDelay.ToString(CultureInfo.InvariantCulture)]);
            encodeArgs.Add(temporary);
            encoder = MediaTools.Start("ffmpeg", encodeArgs, output: false, input: true);
            errors = encoder.StandardError.ReadToEndAsync();
            using var registration = cancellation.Register(() => MediaTools.Stop(encoder));
            progress?.Report(new(0, totalFrames, width, height));
            var written = 0;
            for (var offset = 0; offset < totalFrames; offset++)
            {
                cancellation.ThrowIfCancellationRequested();
                var index = offset % info.FrameCount;
                var rendered = firstRendered;
                if (offset > 0)
                {
                    if (index == 0) sourceId = Guid.NewGuid().ToString("N");
                    rendered = await RenderFrame(index).ConfigureAwait(false);
                }
                cancellation.ThrowIfCancellationRequested();
                PixelFrame? composed = null;
                while (gif == null ? written == offset : written < gif.Count && gif.SourceIndex(written) == index)
                {
                    composed ??= comparison == null ? rendered.Result : OutputComposer.SideBySide(
                        rendered.Source ?? await readFrame(index, cancellation).ConfigureAwait(false), rendered.Result, comparison);
                    frameObserver?.Invoke(index, composed);
                    await encoder.StandardInput.BaseStream.WriteAsync(composed.Pixels, cancellation).ConfigureAwait(false);
                    written++;
                }
                progress?.Report(new(offset + 1, totalFrames, width, height));
            }
            await encoder.StandardInput.BaseStream.FlushAsync(cancellation).ConfigureAwait(false);
            encoder.StandardInput.Close();
            progress?.Report(new(totalFrames, totalFrames, width, height, Finalizing: true));
            await encoder.WaitForExitAsync(cancellation).ConfigureAwait(false);
            var error = await errors.ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (encoder.ExitCode != 0) throw new InvalidOperationException(Localization.F(
                $"Export als {extension.ToUpperInvariant()} fehlgeschlagen: {error.Trim()}"));
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException(Localization.T("Der Encoder hat keine Datei erzeugt."));
            // Only replace the destination after a complete, successful encode.
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, outputPath, overwrite);
            return new(outputPath, written, width, height, gif?.Duration ?? totalFrames / info.FramesPerSecond);
        }
        catch (Exception ex) when (ex is IOException || cancellation.IsCancellationRequested)
        {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
            MediaTools.Stop(encoder);
            if (errors != null)
            {
                var detail = await errors.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(detail)) throw new IOException(extension.ToUpperInvariant() + "-Export: " + detail.Trim(), ex);
            }
            throw;
        }
        finally
        {
            MediaTools.Stop(encoder);
            if (encoder != null)
            {
                try { await encoder.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (InvalidOperationException) { }
                if (errors != null) await errors.ConfigureAwait(false);
                encoder.Dispose();
            }
            try { await DeleteTemporaryAsync(temporary).ConfigureAwait(false); }
            finally
            {
                if (audioCycle != null) await DeleteTemporaryAsync(audioCycle).ConfigureAwait(false);
            }
        }
    }

    private static async Task DeleteTemporaryAsync(string path)
    {
        // Windows scanners can briefly retain a handle after the encoder exits.
        for (int attempt = 0; File.Exists(path); attempt++)
        {
            try { File.Delete(path); break; }
            catch (IOException) when (attempt < 7) { await Task.Delay(50 * (attempt + 1)).ConfigureAwait(false); }
        }
    }

    private const string AudioAlignment = "aresample=async=1:first_pts=0,apad";

    private static async Task CheckAdditionalEncodersAsync(VideoInfo info, VideoExportFormat format, CancellationToken cancellation)
    {
        var (video, audio) = format == VideoExportFormat.ProRes ? ("prores_ks", "pcm_s24le") : ("libvpx-vp9", "libopus");
        var list = await MediaTools.CaptureAsync("ffmpeg", ["-hide_banner", "-encoders"], cancellation).ConfigureAwait(false);
        var names = list.Split('\n').Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(fields => fields.Length >= 2).Select(fields => fields[1]).ToHashSet(StringComparer.Ordinal);
        if (!names.Contains(video))
            throw new InvalidOperationException(Localization.F($"Die ausgewählte FFmpeg-Version enthält den Encoder {video} nicht."));
        if (!names.Contains(audio) && !info.IsStill && await HasAudioAsync(info, cancellation).ConfigureAwait(false))
            throw new InvalidOperationException(Localization.F($"Die ausgewählte FFmpeg-Version enthält den Encoder {audio} nicht."));
    }

    private static async Task<bool> HasAudioAsync(VideoInfo info, CancellationToken cancellation)
    {
        var stream = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-protocol_whitelist", "file,pipe",
            "-select_streams", "a:0", "-show_entries", "stream=index", "-of", "csv=p=0", info.Path], cancellation).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(stream);
    }

    private static async Task<bool> CreateAudioCycleAsync(VideoInfo info, string path, CancellationToken cancellation)
    {
        if (!await HasAudioAsync(info, cancellation).ConfigureAwait(false)) return false;
        // Normalize one pass to the video duration before repeating audio. Source containers can have
        // an audio lead-in or tail. PCM also avoids an AAC encoder delay at every join.
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-copyts",
            "-itsoffset", (-info.StartTime).ToString("R", CultureInfo.InvariantCulture),
            "-protocol_whitelist", "file,pipe", "-i", info.Path, "-map", "0:a:0",
            "-af", AudioAlignment, "-ar", "48000", "-c:a", "pcm_s24le", "-rf64", "auto",
            "-t", info.Duration.ToString("R", CultureInfo.InvariantCulture), path], cancellation).ConfigureAwait(false);
        return true;
    }

    private static void AddVideoOptions(List<string> args, VideoInfo info, VideoExportFormat format, Mp4Quality quality,
        int loops, string? audioCycle)
    {
        var crf = quality switch
        {
            Mp4Quality.Standard => "23",
            Mp4Quality.High => "18",
            Mp4Quality.VeryHigh => "14",
            Mp4Quality.Maximum => "10",
            _ => throw new ArgumentOutOfRangeException(nameof(quality))
        };
        var pixelFormat = format == VideoExportFormat.ProRes ? "yuv422p10le" : "yuv420p";
        var originalAudio = loops == 1 && !info.IsStill;
        if (originalAudio) args.AddRange([
            "-itsoffset", (-info.StartTime).ToString("R", CultureInfo.InvariantCulture),
            "-protocol_whitelist", "file,pipe", "-i", info.Path]);
        if (audioCycle != null) args.AddRange(["-stream_loop", (loops - 1).ToString(CultureInfo.InvariantCulture), "-i", audioCycle]);
        args.AddRange(["-map", "0:v:0"]);
        if (originalAudio || audioCycle != null) args.AddRange(["-map", "1:a:0?"]);
        args.AddRange(["-map_metadata", "-1", "-map_chapters", "-1",
            "-vf", $"pad=ceil(iw/2)*2:ceil(ih/2)*2,scale=in_range=full:out_range=tv:out_color_matrix=bt709,format={pixelFormat},setparams=range=limited:color_primaries=bt709:color_trc=bt709:colorspace=bt709",
            "-pix_fmt", pixelFormat,
            "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", "-color_range", "tv",
            "-threads", "2", "-fps_mode", "passthrough"]);
        switch (format)
        {
            case VideoExportFormat.ProRes:
                args.AddRange(["-c:v", "prores_ks", "-profile:v", "standard"]);
                break;
            case VideoExportFormat.WebM:
                args.AddRange(["-c:v", "libvpx-vp9", "-b:v", "0", "-crf", crf, "-row-mt", "1", "-cpu-used", "2"]);
                break;
            default:
                args.AddRange(["-c:v", format == VideoExportFormat.Hevc ? "libx265" : "libx264", "-preset", "medium", "-crf", crf]);
                if (format == VideoExportFormat.Hevc) args.AddRange(["-tag:v", "hvc1", "-x265-params", "pools=2:frame-threads=2"]);
                break;
        }
        if (originalAudio || audioCycle != null)
        {
            args.AddRange(format switch
            {
                VideoExportFormat.ProRes => ["-c:a", "pcm_s24le"],
                VideoExportFormat.WebM => ["-c:a", "libopus", "-b:a", "192k"],
                _ => ["-c:a", "aac", "-b:a", "192k"]
            });
            args.AddRange(["-af", AudioAlignment]);
        }
        args.AddRange(["-t", (checked(info.FrameCount * loops) / info.FramesPerSecond).ToString("R", CultureInfo.InvariantCulture)]);
        if (format != VideoExportFormat.WebM) args.AddRange(["-movflags", "+faststart"]);
    }

    internal const int GifMaxAutomaticSide = 480;
    internal static int Mp4Dimension(int size) => (size + 1) & ~1;

    internal static OutputSettings ComparisonOutput(int processedWidth, int processedHeight, OutputSettings? requested,
        bool gif = false, int? gifWidth = null)
    {
        var width = requested?.Width ?? 2 * processedWidth;
        var height = requested?.Height ?? processedHeight;
        var scale = gif && gifWidth is int target ? target / (double)width
            : gif && requested == null ? Math.Min(1, GifMaxAutomaticSide / (double)Math.Max(width, height)) : 1;
        scale = Math.Min(scale, Math.Min(OutputSettings.MaxSide / (double)Math.Max(width, height),
            Math.Sqrt(OutputSettings.MaxPixels / ((double)width * height))));
        return new(Math.Max(2, (int)Math.Floor(width * scale + 1e-8)), Math.Max(1, (int)Math.Floor(height * scale + 1e-8)));
    }

    internal static RenderSettings ConfigureGifOutput(int width, int height, RenderSettings settings, GifExportSettings? options)
    {
        var (output, limit) = GifOutput(width, height, settings.Output, settings.Crop, options?.Width);
        return settings with { Output = output, ScanlineSizeLimit = limit };
    }

    internal static GifEstimate EstimateGif(VideoInfo info, OutputSettings? requested, SourceCrop? crop,
        GifExportSettings options, bool snap, int scanlineRows)
    {
        var (output, limit) = GifOutput(info.Width, info.Height, requested, crop, options.Width);
        if (snap)
        {
            var rect = crop?.Rectangle(info.Width, info.Height);
            var width = rect?.Width ?? info.Width;
            var height = rect?.Height ?? info.Height;
            var layout = output.ScanlineGeometry(width, height, scanlineRows > 0 ? scanlineRows : height, limit);
            output = output with { Width = layout.Width, Height = layout.Height };
        }
        return new(output.Width, output.Height, new GifTiming(info, options.Fps).Count);
    }

    private static (OutputSettings Output, int Limit) GifOutput(int sourceWidth, int sourceHeight,
        OutputSettings? requested, SourceCrop? crop, int? requestedWidth)
    {
        if (requestedWidth == null && requested != null) return (requested, OutputSettings.MaxSide);
        var rect = crop?.Rectangle(sourceWidth, sourceHeight);
        var width = rect?.Width ?? sourceWidth;
        var height = rect?.Height ?? sourceHeight;
        var scale = requestedWidth is int target
            ? Math.Min(target / (double)width, OutputSettings.MaxSide / (double)height)
            : Math.Min(1, GifMaxAutomaticSide / (double)Math.Max(width, height));
        return (new(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale))),
            requestedWidth == null ? GifMaxAutomaticSide : OutputSettings.MaxSide);
    }

    internal sealed class GifTiming(VideoInfo info, int? requestedFps)
    {
        private readonly int _autoDelay = Math.Max(8, (int)Math.Round(100 / info.FramesPerSecond));
        public double Fps => requestedFps ?? 100.0 / _autoDelay;
        public int Count => Math.Max(1, checked((int)Math.Round(info.Duration * Fps, MidpointRounding.AwayFromZero)));
        // GIF timestamps use hundredths of a second. Intermediate delays can alternate
        // to preserve the average rate. The muxer needs the last delay explicitly.
        private long Timestamp(int index) => (long)Math.Round(index * 100.0 / Fps, MidpointRounding.AwayFromZero);
        public int FinalDelay => (int)(Timestamp(Count) - Timestamp(Count - 1));
        public double Duration => Timestamp(Count) / 100.0;
        public string Rate => requestedFps?.ToString(CultureInfo.InvariantCulture) ?? FormattableString.Invariant($"100/{_autoDelay}");
        public int SourceIndex(int index) => Math.Min(info.FrameCount - 1,
            (int)Math.Floor(index / Fps * info.FramesPerSecond + 1e-9));
    }
}
