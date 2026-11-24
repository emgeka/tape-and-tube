// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class EffectTimelineSmoke
{
    public static async Task RunAsync(string directory, VideoSource source, Action<bool, string> check, CancellationToken token)
    {
        static bool Same(byte[] left, byte[] right) => left.AsSpan().SequenceEqual(right);
        var shader = "crt-easymode";
        var analog = AnalogCatalog.Current.Defaults();
        var ntsc = NtscProcessor.Defaults();
        var first = new EffectKeyframe(0, "Hold", shader, [], ntsc, new(analog), DigitalCatalog.Defaults);
        var last = first with { T = 1, Glitch = new(analog) { ["signal_strength"] = 0.05 } };
        var track = new EffectTimeline(true, 0.5, [first, last]);
        var stored = new LookPreset("tape-and-tube", 4, false, false, shader, 320, 3, ntsc, [],
            Renderer: DigitalCatalog.Profile, Analog: new(false, true, analog), Timeline: track);
        var loaded = LookPreset.Parse(JsonSerializer.Serialize(stored with { Timeline = track with { Fps = 24 } }), [shader]);
        check(loaded.Timeline is { Enabled: true, Seconds: 0.5, Fps: 24, Keys: [_, _] } && loaded.Timeline.Keys[1].Glitch["signal_strength"] == 0.05,
            "A Tape & Tube look round-trips its own keyframes");
        var old = JsonSerializer.SerializeToNode(stored)!.AsObject();
        old["Timeline"]!.AsObject().Remove("Fps");
        check(LookPreset.Parse(old.ToJsonString(), [shader]).Timeline?.Fps == 30,
            "Saved animations without a frame rate retain the 30 fps default");

        var still = PixelFrame.TestPattern();
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], new(144, 96),
            DigitalCatalog.Profile, Analog: new(false, true, analog), EffectTimeline: track.WorkerTimeline(shader));
        var time = ScreenLoopCatalog.StillTimeline(still, track.Seconds);
        using var pipeline = new EffectPipeline();
        async Task<byte[]> RenderStill(int i, RenderSettings look) => (await pipeline.RenderAsync(still, look with { Frame = (uint)i },
            "editor-still", time, cancellation: token)).Frame.Pixels;
        var start = await RenderStill(0, settings);
        var middle = await RenderStill(7, settings);
        var end = await RenderStill(14, settings);
        var blend = track with { Keys = [first with { Easing = "Linear" }, last] };
        var blendedMiddle = await RenderStill(7, settings with { EffectTimeline = blend.WorkerTimeline(shader) });
        check(Same(start, middle) && !Same(start, end) && !Same(middle, blendedMiddle),
            "Own keyframes keep or interpolate the captured reception values according to easing");
        check(Same(end, await RenderStill(14, settings)) && Same(middle, await RenderStill(7, settings)),
            "Seeking within a still animation reproduces its preview");

        var stillExport = await VideoExporter.ExportStillAsync(still, null, Path.Combine(directory, "editor-still.mp4"),
            track.Seconds, settings, cancellation: token, overwrite: true, frameObserver: (i, frame) =>
            {
                if ((i == 7 && !Same(middle, frame.Pixels)) || (i == 14 && !Same(end, frame.Pixels)))
                    throw new InvalidDataException("Still keyframe export differs from preview.");
            });
        check(stillExport.Frames == 15, "A still animation authored in Tape & Tube exports at the expected rate");

        var frames = new List<byte[]>();
        for (var i = 0; i < source.Info.FrameCount; i++)
        {
            var sourceFrame = await source.ReadFrameAsync(i, token);
            frames.Add((await pipeline.RenderAsync(sourceFrame, settings with { Frame = (uint)i }, "editor-video",
                source.Info, source.ReadFrameAsync, token)).Frame.Pixels);
        }
        var output = Path.Combine(directory, "editor-video.mp4");
        var videoExport = await VideoExporter.ExportAsync(source.Info.Path, output, settings, cancellation: token,
            overwrite: true, frameObserver: (i, frame) =>
            {
                if (!Same(frames[i], frame.Pixels)) throw new InvalidDataException("Video keyframe export differs from preview.");
            });
        var audio = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-select_streams", "a:0",
            "-show_entries", "stream=codec_name", "-of", "csv=p=0", output], token);
        check(videoExport.Frames == frames.Count && audio.Trim() == "aac", "Own video keyframes match preview and keep the video audio");
    }
}
