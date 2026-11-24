// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class ScreenLoopSmoke
{
    public static async Task RunAsync(string directory, VideoSource source, Action<bool, string> check, CancellationToken token)
    {
        static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        var catalog = ScreenLoopCatalog.Current;
        foreach (var entry in catalog.Presets) catalog.Validate(entry.Preset.Values);
        check(catalog.Presets.Length == 5 && catalog.Parameters.Length == 23, "Screen Loop exposes all 23 controls and five NTSCRT presets");
        var values = catalog.Defaults();
        values["zoom"] = 0.62;
        values["delay"] = 2;
        values["focus"] = 0;
        values["push"] = 0.12;
        values["key"] = 0.3;
        var settings = new RenderSettings(false, true, "{}", Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp"),
            160, 2, 0, [], new(144, 96), DigitalCatalog.Profile, ScreenLoop: values);
        var preset = new ScreenLoopPreset(ScreenLoopPreset.PresetKind, 2, values, 0.5);
        var parsed = ScreenLoopPreset.Parse(JsonSerializer.Serialize(preset, CoreWorkerClient.Json));
        check(parsed.Seconds == 0.5 && parsed.Values.OrderBy(p => p.Key).SequenceEqual(values.OrderBy(p => p.Key)),
            "Screen Loop preset round-trips settings and standstill duration in NTSCRT's version 2 format");

        using var pipeline = new EffectPipeline();
        async Task<byte[]> Render(int index, RenderSettings look)
        {
            var pixels = await source.ReadFrameAsync(index, token);
            return (await pipeline.RenderAsync(pixels, look with { Frame = (uint)index }, "screen-loop-clip",
                source.Info, source.ReadFrameAsync, token)).Frame.Pixels;
        }
        var dry = await Render(0, settings with { ScreenLoop = null });
        var expected = new List<byte[]>();
        for (var i = 0; i < source.Info.FrameCount; i++) expected.Add(await Render(i, settings));
        check(!Same(dry, expected[0]) && expected[0].Where((_, i) => i % 4 != 3).Any(b => b > 10),
            "Screen Loop starts with a visible warmed-up tunnel on the first frame");
        check(!Same(expected[0], expected[9]), "The feedback evolves with the source and camera movement");
        check(Same(expected[3], await Render(3, settings)) && Same(expected[9], await Render(9, settings))
            && Same(expected[0], await Render(0, settings)), "Forward and backward seeks and loop restart match uninterrupted feedback");
        check(Same(expected[0], await Render(0, settings with { Digital = DigitalCatalog.Find("webcam").Values })),
            "Screen Loop temporarily bypasses Digital video");
        var nativeLoop = await Render(0, settings with { DownscaleEnabled = false });
        check(nativeLoop.Where((_, i) => i % 4 != 3).Any(b => b > 10) && !Same(nativeLoop, expected[0]),
            "Screen Loop warms up at source resolution with internal downscale disabled");

        var videoOutput = Path.Combine(directory, "screen-loop-video.mp4");
        var sameVideo = true;
        var result = await VideoExporter.ExportAsync(source.Info.Path, videoOutput, settings, cancellation: token, overwrite: true,
            frameObserver: (n, frame) => sameVideo &= Same(expected[n], frame.Pixels));
        check(sameVideo && result.Frames == expected.Count, "Video Screen Loop preview and export match before H.264 encoding");
        var audio = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-select_streams", "a:0", "-show_entries",
            "stream=codec_name", "-of", "csv=p=0", videoOutput], token);
        check(audio.Trim() == "aac", "Video Screen Loop export retains audio");

        var still = PixelFrame.TestPattern();
        var timeline = ScreenLoopCatalog.StillTimeline(still, 0.5);
        var first = await pipeline.RenderAsync(still, settings, "still-screen-loop", timeline, cancellation: token);
        var last = await pipeline.RenderAsync(still, settings with { Frame = 14 }, "still-screen-loop", timeline, cancellation: token);
        var stillOutput = Path.Combine(directory, "screen-loop-still.mp4");
        var matchedStill = true;
        var stillResult = await VideoExporter.ExportStillAsync(still, null, stillOutput, 0.5, settings, cancellation: token, overwrite: true,
            frameObserver: (i, frame) =>
            {
                if (i == 0) matchedStill &= Same(first.Frame.Pixels, frame.Pixels);
                if (i == 14) matchedStill &= Same(last.Frame.Pixels, frame.Pixels);
            });
        await using var decoded = await VideoSource.OpenAsync(stillOutput, token);
        check(matchedStill && stillResult.Frames == 15 && decoded.Info.FrameCount == 15 && !Same(first.Frame.Pixels, last.Frame.Pixels),
            "A half-second still produces fifteen animated frames matching preview");
    }
}
