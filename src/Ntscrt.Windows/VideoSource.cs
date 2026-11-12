// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal sealed record VideoInfo(string Path, int Width, int Height, double FramesPerSecond, string Rate, int FrameCount, double StartTime)
{
    public double Duration => FrameCount / FramesPerSecond;
}

/// Streaming decoder with a bounded frame cache. Backward seeks restart the decoder;
/// trimming after FPS normalization makes sequential reads and seeks agree exactly.
internal sealed class VideoSource : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _lifetime = new();
    private Process? _decoder;
    private Task<string>? _errors;
    private int _nextFrame;
    private int _cachedIndex = -1;
    private PixelFrame? _cached;
    public VideoInfo Info { get; }

    private VideoSource(VideoInfo info) => Info = info;
    public static async Task<VideoSource> OpenAsync(string path, CancellationToken cancellation = default)
    {
        path = System.IO.Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException(Localization.T("Video wurde nicht gefunden."), path);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var output = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-protocol_whitelist", "file,pipe", "-select_streams", "v:0",
            "-show_streams", "-show_format", "-of", "json", path], timeout.Token);
        using var json = JsonDocument.Parse(output);
        var streams = json.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidDataException(Localization.T("Die Datei enthält keine Videospur."));
        var stream = streams[0];
        var width = stream.GetProperty("width").GetInt32();
        var height = stream.GetProperty("height").GetInt32();
        var rate = Text(stream, "avg_frame_rate");
        var fps = Ratio(rate);
        if (!double.IsFinite(fps) || fps <= 0) { rate = Text(stream, "r_frame_rate"); fps = Ratio(rate); }
        var duration = Number(stream, "duration");
        if (duration <= 0 && json.RootElement.TryGetProperty("format", out var format)) duration = Number(format, "duration");
        if (!double.IsFinite(fps) || fps < 0.1 || fps > 240 || !double.IsFinite(duration) || duration <= 0 || duration * fps > int.MaxValue - 1)
            throw new InvalidDataException(Localization.T("Bildrate oder Videolänge wird nicht unterstützt."));
        var sar = Ratio(Text(stream, "sample_aspect_ratio").Replace(':', '/'));
        if (!double.IsFinite(sar) || sar <= 0) sar = 1;
        width = checked((int)Math.Round(width * sar));
        if (stream.TryGetProperty("side_data_list", out var sides))
            foreach (var side in sides.EnumerateArray())
                if (side.TryGetProperty("rotation", out var rotation) && Math.Abs(rotation.GetDouble() % 180) > 45)
                { (width, height) = (height, width); break; }
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384 || (long)width * height > 24_000_000)
            throw new InvalidDataException(Localization.T("Das Video überschreitet die unterstützte Bildgröße (24 Megapixel)."));
        var frames = Math.Max(1, checked((int)Math.Round(duration * fps)));
        var startTime = Number(stream, "start_time");
        if (!double.IsFinite(startTime)) startTime = 0;
        return new(new(path, width, height, fps, rate, frames, startTime));
    }

    private static string Text(JsonElement element, string property) => element.TryGetProperty(property, out var value) ? value.ToString() : "";
    private static double Number(JsonElement element, string property) => double.TryParse(Text(element, property), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : 0;
    private static double Ratio(string text)
    {
        var terms = text.Split('/');
        return double.TryParse(terms[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            ? terms.Length == 2 && double.TryParse(terms[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? n / d : n
            : double.NaN;
    }

    public async Task<PixelFrame> ReadFrameAsync(int index, CancellationToken cancellation = default)
    {
        if (index < 0 || index >= Info.FrameCount) throw new ArgumentOutOfRangeException(nameof(index));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
        await _gate.WaitAsync(linked.Token);
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (_cachedIndex == index && _cached != null) return _cached;
            if (_decoder == null || index < _nextFrame)
            {
                StopDecoder();
                _decoder = MediaTools.Start("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-threads", "2",
                    "-protocol_whitelist", "file,pipe", "-i", Info.Path, "-map", "0:v:0", "-an", "-sn", "-dn", "-filter_threads", "1",
                    "-vf", $"setpts=PTS-STARTPTS,fps={Info.Rate},scale={Info.Width}:{Info.Height},setsar=1,trim=start_frame={index}",
                    "-fps_mode", "passthrough", "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1"]);
                _errors = _decoder.StandardError.ReadToEndAsync();
                _nextFrame = index;
            }
            using var registration = linked.Token.Register(() => MediaTools.Stop(_decoder));
            var bytes = new byte[checked(Info.Width * Info.Height * 4)];
            while (_nextFrame <= index)
            {
                linked.Token.ThrowIfCancellationRequested();
                try { await _decoder.StandardOutput.BaseStream.ReadExactlyAsync(bytes, linked.Token); }
                catch (EndOfStreamException)
                {
                    linked.Token.ThrowIfCancellationRequested();
                    await _decoder.WaitForExitAsync(linked.Token);
                    var error = await _errors!;
                    throw new InvalidDataException(string.IsNullOrWhiteSpace(error) ? Localization.T("Das Video endet vor dem angeforderten Bild.") : error.Trim());
                }
                _nextFrame++;
            }
            _cachedIndex = index;
            _cached = new(Info.Width, Info.Height, bytes);
            return _cached;
        }
        catch { StopDecoder(); throw; }
        finally { _gate.Release(); }
    }

    private void StopDecoder()
    {
        MediaTools.Stop(_decoder);
        _decoder?.Dispose(); _decoder = null; _errors = null; _nextFrame = 0;
    }
    public void Cancel() { _lifetime.Cancel(); MediaTools.Stop(_decoder); }
    public async ValueTask DisposeAsync()
    {
        Cancel();
        await _gate.WaitAsync();
        try { StopDecoder(); _cached = null; _cachedIndex = -1; }
        finally { _gate.Release(); }
    }
}
