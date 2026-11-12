// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Ntscrt.Windows;

internal sealed record ExportProgress(int CompletedFrames, int TotalFrames, int Width, int Height, bool Finalizing = false);
internal sealed record ExportResult(string Path, int Frames, int Width, int Height, double Duration);

internal static class VideoExporter
{
    public static async Task<ExportResult> ExportAsync(string sourcePath, string outputPath, RenderSettings settings,
        IProgress<ExportProgress>? progress = null, CancellationToken cancellation = default, bool overwrite = false,
        Action<int, PixelFrame>? frameObserver = null)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        outputPath = Path.GetFullPath(outputPath);
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(Localization.T("Bitte einen anderen Dateinamen wählen. Das Quellvideo wird nicht überschrieben."));
        if (File.Exists(outputPath) && !overwrite) throw new IOException(Localization.T("Die Zieldatei existiert bereits."));
        var directory = Path.GetDirectoryName(outputPath)!;
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(Localization.T("Der Zielordner existiert nicht."));
        // Freeze caller-owned values, including the parameter dictionary, for the whole job.
        settings = settings with { ShaderValues = new(settings.ShaderValues) };
        cancellation.ThrowIfCancellationRequested();
        await using var source = await VideoSource.OpenAsync(sourcePath, cancellation).ConfigureAwait(false);
        using var pipeline = new EffectPipeline();
        var first = await source.ReadFrameAsync(0, cancellation).ConfigureAwait(false);
        cancellation.ThrowIfCancellationRequested();
        var firstRendered = pipeline.Render(first, settings with { Frame = 0 }).Frame;
        var width = (firstRendered.Width + 1) & ~1;
        var height = (firstRendered.Height + 1) & ~1;
        var info = source.Info;
        var temporary = Path.Combine(directory, $".ntscrt-{Guid.NewGuid():N}.mp4");
        Process? encoder = null;
        Task<string>? errors = null;
        try
        {
            encoder = MediaTools.Start("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-copyts",
                "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", $"{firstRendered.Width}x{firstRendered.Height}",
                "-framerate", info.Rate, "-i", "pipe:0",
                "-itsoffset", (-info.StartTime).ToString("R", CultureInfo.InvariantCulture),
                "-protocol_whitelist", "file,pipe", "-i", sourcePath,
                "-map", "0:v:0", "-map", "1:a:0?", "-map_metadata", "-1", "-map_chapters", "-1",
                "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2,scale=in_range=full:out_range=tv:out_color_matrix=bt709,format=yuv420p",
                "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-pix_fmt", "yuv420p",
                "-color_primaries", "bt709", "-color_trc", "bt709", "-colorspace", "bt709", "-color_range", "tv",
                "-threads", "2", "-fps_mode", "passthrough",
                "-c:a", "aac", "-b:a", "192k", "-af", "aresample=async=1:first_pts=0,apad",
                "-t", info.Duration.ToString("R", CultureInfo.InvariantCulture), "-movflags", "+faststart", temporary], output: false, input: true);
            errors = encoder.StandardError.ReadToEndAsync();
            using var registration = cancellation.Register(() => MediaTools.Stop(encoder));
            progress?.Report(new(0, info.FrameCount, width, height));
            for (var index = 0; index < info.FrameCount; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                var rendered = firstRendered;
                if (index > 0)
                {
                    var decoded = await source.ReadFrameAsync(index, cancellation).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    rendered = pipeline.Render(decoded, settings with { Frame = (uint)index }).Frame;
                }
                cancellation.ThrowIfCancellationRequested();
                frameObserver?.Invoke(index, rendered);
                await encoder.StandardInput.BaseStream.WriteAsync(rendered.Pixels, cancellation).ConfigureAwait(false);
                progress?.Report(new(index + 1, info.FrameCount, width, height));
            }
            await encoder.StandardInput.BaseStream.FlushAsync(cancellation).ConfigureAwait(false);
            encoder.StandardInput.Close();
            progress?.Report(new(info.FrameCount, info.FrameCount, width, height, Finalizing: true));
            await encoder.WaitForExitAsync(cancellation).ConfigureAwait(false);
            var error = await errors.ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();
            if (encoder.ExitCode != 0) throw new InvalidOperationException(Localization.T("MP4 konnte nicht gespeichert werden: ") + error.Trim());
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException(Localization.T("Der Encoder hat keine MP4-Datei erzeugt."));
            // Only replace the destination after a complete, successful encode.
            cancellation.ThrowIfCancellationRequested();
            File.Move(temporary, outputPath, overwrite);
            return new(outputPath, info.FrameCount, width, height, info.Duration);
        }
        catch (Exception ex) when (ex is IOException || cancellation.IsCancellationRequested)
        {
            if (cancellation.IsCancellationRequested) throw new OperationCanceledException(cancellation);
            MediaTools.Stop(encoder);
            if (errors != null)
            {
                var detail = await errors.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(detail)) throw new IOException("MP4-Export: " + detail.Trim(), ex);
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
            // Windows scanners can briefly retain a handle after the encoder exits.
            for (int attempt = 0; File.Exists(temporary); attempt++)
            {
                try { File.Delete(temporary); break; }
                catch (IOException) when (attempt < 7) { await Task.Delay(50 * (attempt + 1)).ConfigureAwait(false); }
            }
        }
    }
}
