// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class GifSmoke
{
    public static async Task RunStandaloneAsync(string directory)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var source = Path.Combine(directory, "video-with-audio.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=96x64:rate=6:duration=2", "-f", "lavfi", "-i", "sine=duration=2",
            "-c:v", "mpeg4", "-c:a", "aac", source], timeout.Token);
        var checks = new List<string>();
        await RunAsync(directory, source, (passed, label) =>
        {
            if (!passed) throw new InvalidDataException(label);
            checks.Add(label);
        }, timeout.Token);
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new { passed = true, checks }));
    }

    public static async Task RunAsync(string directory, string source, Action<bool, string> check, CancellationToken token)
    {
        static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        var settings = new RenderSettings(false, false, "{}", "", 96, 2, 0, [],
            new(65, 49), Crop: new("1:1", X: 1));
        var videoPath = Path.Combine(directory, "cropped-video.gif");
        var video = await VideoExporter.ExportAsync(source, videoPath, settings, cancellation: token, overwrite: true,
            format: VideoExportFormat.Gif);
        var metadata = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-show_entries",
            "stream=codec_type,codec_name,width,height,nb_frames,duration", "-of", "json", videoPath], token);
        using var document = JsonDocument.Parse(metadata);
        var streams = document.RootElement.GetProperty("streams");
        var stream = streams[0];
        check(streams.GetArrayLength() == 1 && stream.GetProperty("codec_name").GetString() == "gif" &&
            stream.GetProperty("width").GetInt32() == 65 && stream.GetProperty("height").GetInt32() == 49 &&
            stream.GetProperty("nb_frames").GetString() == video.Frames.ToString(CultureInfo.InvariantCulture) && video.Frames == 12 &&
            Math.Abs(double.Parse(stream.GetProperty("duration").GetString()!, CultureInfo.InvariantCulture) - video.Duration) < 0.01,
            "GIF from a video preserves odd output dimensions, duration and a single image stream");
        var loop = Encoding.ASCII.GetBytes("NETSCAPE2.0\x03\x01\x00\x00");
        check(File.ReadAllBytes(videoPath).AsSpan().IndexOf(loop) >= 0, "Animated GIF repeats indefinitely");

        var still = PixelFrame.TestPattern();
        var analog = AnalogCatalog.Current.Defaults();
        var first = new EffectKeyframe(0, "Linear", "crt-easymode", [], NtscProcessor.Defaults(), new(analog), DigitalCatalog.Defaults);
        var last = first with { T = 1, Glitch = new(analog) { ["signal_strength"] = 0.05 } };
        var track = new EffectTimeline(true, 1, [first, last]);
        var stillPath = Path.Combine(directory, "animated-still.gif");
        var stillGif = await VideoExporter.ExportStillAsync(still, null, stillPath, track.Seconds,
            settings with { Output = null, Renderer = DigitalCatalog.Profile, Analog = new(false, true, analog),
                EffectTimeline = track.WorkerTimeline(first.ShaderName) }, cancellation: token, overwrite: true,
            format: VideoExportFormat.Gif);
        await using var decoded = await VideoSource.OpenAsync(stillPath, token);
        var start = await decoded.ReadFrameAsync(0, token);
        var end = await decoded.ReadFrameAsync(decoded.Info.FrameCount - 1, token);
        check(decoded.Info.Width == 240 && decoded.Info.Height == 240 && decoded.Info.FrameCount == stillGif.Frames &&
            stillGif.Frames == 13 && Math.Abs(decoded.Info.Duration - 1.04) < 0.02 && !Same(start.Pixels, end.Pixels),
            "GIF from a still keeps the crop and keyframe motion at a valid GIF frame delay");
    }
}
