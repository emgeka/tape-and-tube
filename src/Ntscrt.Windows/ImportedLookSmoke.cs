// SPDX-License-Identifier: GPL-3.0-only
using System.IO;

namespace Ntscrt.Windows;

internal static class ImportedLookSmoke
{
    public const string Minimal = """
        {"version":2,"downscale":{"enabled":false},"ntsc":{"enabled":false},"shader":{"enabled":false},
         "glitch":{"enabled":true,"values":{"signal_strength":1}},
         "timeline":{"enabled":true,"duration":0.5,"fps":12,
           "keys":[{"t":0,"easing":"Hold","glitch":{"signal_strength":1}},
                   {"t":1,"easing":"Linear","glitch":{"signal_strength":0.05}}]}}
        """;

    public static async Task RunAsync(string directory, VideoSource source, Action<bool, string> check, CancellationToken token)
    {
        static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        var upstream = Path.Combine(Paths.Root, "prototypes", "processing-core", "upstream");
        var pins = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(upstream, "..", "upstream-sources.json")))!;
        var originalPresets = Path.Combine(upstream, pins["ntscrt"]!["directory"]!.GetValue<string>());
        var files = Directory.GetFiles(Path.Combine(originalPresets, "presets"), "*.json");
        var settings = new RenderSettings(false, false, "{}", "", 320, 2, 0, [], new(144, 96), DigitalCatalog.Profile);
        using var preview = new EffectPipeline();
        var still = PixelFrame.TestPattern();
        foreach (var path in files)
        {
            var look = NtscrtImportedLook.Parse(File.ReadAllText(path));
            if (look == null) throw new InvalidDataException($"Not an NTSCRT look: {path}");
            try
            {
                var frame = await preview.RenderAsync(still, settings with { Crt = look.Crt, RetroWidth = look.RetroWidth, ImportedLook = look },
                    "original-looks", cancellation: token);
                if (frame.Frame.Width != 144 || frame.Frame.Height != 96)
                    throw new InvalidOperationException("Unexpected dimensions.");
                if (Path.GetFileName(path) == "snappy jerks w CRT 2 seconds.json")
                    check(look.KeyframeCount == 24 && frame.Frame.Pixels.Where((_, i) => i % 4 != 3).Any(value => value > 8),
                        "The version 3 Aperture look retains its animation and a visible picture at 144 by 96");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"NTSCRT look {Path.GetFileName(path)} failed to render.", ex);
            }
        }
        check(files.Length >= 44, $"All {files.Length} original NTSCRT looks open and render");

        var dirtyTape = NtscrtImportedLook.Parse(File.ReadAllText(Path.Combine(originalPresets, "presets", "2 second dirty VCR loop.json")))!;
        check(dirtyTape is { KeyframeCount: 15, Fps: 24, Seconds: 2 }, "NTSCRT's dirty VCR look retains its original timeline");
        var videoLook = settings with { Crt = dirtyTape.Crt, RetroWidth = dirtyTape.RetroWidth, ImportedLook = dirtyTape };
        async Task<byte[]> RenderVideo(int index)
        {
            var pixels = await source.ReadFrameAsync(index, token);
            return (await preview.RenderAsync(pixels, videoLook with { Frame = (uint)index }, "look-video",
                source.Info, source.ReadFrameAsync, token)).Frame.Pixels;
        }
        var frames = new List<byte[]>();
        for (var i = 0; i < source.Info.FrameCount; i++) frames.Add(await RenderVideo(i));
        check(Same(frames[^1], await RenderVideo(frames.Count - 1)) && Same(frames[2], await RenderVideo(2))
            && Same(frames[^1], await RenderVideo(frames.Count - 1)),
            "Seeking an imported animated video look matches uninterrupted preview");
        var videoOutput = Path.Combine(directory, "imported-look-video.mp4");
        var videoMatches = true;
        var videoResult = await VideoExporter.ExportAsync(source.Info.Path, videoOutput, videoLook, cancellation: token,
            overwrite: true, frameObserver: (i, frame) => videoMatches &= Same(frames[i], frame.Pixels));
        check(videoMatches && videoResult.Frames == frames.Count, "An original animated VCR look matches between preview and video export");

        var animation = NtscrtImportedLook.Parse(Minimal)!;
        var version4 = NtscrtImportedLook.Parse(Minimal.Replace("\"version\":2", "\"version\":4"))!;
        check(version4 is { KeyframeCount: 2, Fps: 12, Seconds: 0.5 } && version4.Json.Contains("\"version\":4"),
            "NTSCRT look version 4 is accepted with its original animation and revision");
        var stillLook = settings with { ImportedLook = animation };
        var timeline = ScreenLoopCatalog.StillTimeline(still, animation.Seconds, animation.Fps);
        var stillFrames = new List<byte[]>();
        for (var i = 0; i < timeline.FrameCount; i++)
        {
            var frame = await preview.RenderAsync(still, stillLook with { Frame = (uint)i }, "look-still", timeline, cancellation: token);
            stillFrames.Add(frame.Frame.Pixels);
        }
        var disabled = NtscrtImportedLook.Parse(Minimal.Replace("\"enabled\":true,\"duration\"", "\"enabled\":false,\"duration\""))!;
        var staticEnd = await preview.RenderAsync(still, stillLook with { Frame = 5, ImportedLook = disabled }, "look-still",
            timeline, cancellation: token);
        check(Same(stillFrames[0], stillFrames[4]) && !Same(stillFrames[^1], staticEnd.Frame.Pixels) && disabled.KeyframeCount == 0,
            "Hold leaves the source unchanged until the final key and a disabled timeline leaves the base settings active");
        var stillOutput = Path.Combine(directory, "imported-look-still.mp4");
        var stillMatches = true;
        var stillResult = await VideoExporter.ExportStillAsync(still, null, stillOutput, animation.Seconds, stillLook,
            cancellation: token, overwrite: true, frameObserver: (i, frame) => stillMatches &= Same(stillFrames[i], frame.Pixels), fps: animation.Fps);
        check(stillMatches && stillResult.Frames == 6 && timeline.Rate == "12/1", "Imported still animation uses its own frame rate when exported");
    }
}
