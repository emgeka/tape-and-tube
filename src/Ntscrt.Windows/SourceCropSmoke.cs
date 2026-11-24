// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Ntscrt.Windows;

internal static class SourceCropSmoke
{
    public static async Task RunAsync(string directory, VideoSource source, Action<bool, string> check, CancellationToken token)
    {
        static bool Same(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        var left = new SourceCrop("1:1", X: 0);
        var right = left with { X = 1 };
        var portrait = new SourceCrop("9:16");
        check(portrait.Rectangle(1920, 1080) == new Int32Rect(656, 0, 608, 1080) &&
            (right with { Y = 1, Zoom = 2 }).Rectangle(640, 480) == new Int32Rect(400, 240, 240, 240),
            "Source crop matches original ratio, placement and zoom geometry");
        check(new SourceCrop("1.85:1").Rectangle(2390, 1000) == new Int32Rect(270, 0, 1850, 1000) &&
            new SourceCrop("2.39:1").Rectangle(2390, 1200) == new Int32Rect(0, 100, 2390, 1000),
            "Cinema crop ratios accept decimal labels and keep the exact ratio");
        var still = PixelFrame.TestPattern();
        var settings = new RenderSettings(false, false, "{}", "", 96, 2, 0, [], Crop: left);
        using var pipeline = new EffectPipeline();
        var legacy = pipeline.Render(still, settings).Frame;
        var shared = settings with { Renderer = DigitalCatalog.Profile };
        var sharedLeft = (await pipeline.RenderAsync(still, shared, "crop-still", cancellation: token)).Frame;
        var sharedRight = (await pipeline.RenderAsync(still, shared with { Crop = right }, "crop-still", cancellation: token)).Frame;
        check(legacy.Width == 240 && legacy.Height == 240 && Same(legacy.Pixels, left.Apply(still).Pixels)
            && Same(legacy.Pixels, sharedLeft.Pixels) && Same(right.Apply(still).Pixels, sharedRight.Pixels)
            && !Same(sharedLeft.Pixels, sharedRight.Pixels),
            "Moving a crop with unchanged dimensions updates both renderers without reusing a stale frame");

        var preset = new LookPreset("tape-and-tube", 5, false, false, "crt-easymode", 320, 3, NtscProcessor.Defaults(), [], Crop: right);
        var reopened = LookPreset.Parse(JsonSerializer.Serialize(preset), [preset.Shader]);
        check(reopened.Crop == right, "Tape & Tube presets preserve the source crop");

        const string original = """
          {"version":3,"downscale":{"enabled":false},"ntsc":{"enabled":false},"shader":{"enabled":false},
           "crop":{"ratio":"1:1","position":1}}
          """;
        var imported = NtscrtImportedLook.Parse(original)!;
        var importedFrame = (await pipeline.RenderAsync(still,
            shared with { ImportedLook = imported, Crop = imported.Crop }, "imported-crop", cancellation: token)).Frame;
        check(imported.Crop == (right with { Y = 1 }) && Same(importedFrame.Pixels, sharedRight.Pixels),
            "An NTSCRT look with a legacy position crops the source once");

        var format = DigitalCatalog.Find("minidv");
        var videoSettings = shared with { RetroWidth = format.Width, Digital = new(format.Values) { ["fields"] = 2 },
            Crop = new("1:1", X: 1, Zoom: 1.3) };
        var crop = videoSettings.Crop!;
        async Task<PixelFrame> ReadCropped(int i, CancellationToken cancel) => crop.Apply(await source.ReadFrameAsync(i, cancel));
        var expected = new List<byte[]>();
        for (var i = 0; i < source.Info.FrameCount; i++)
        {
            var manual = await pipeline.RenderAsync(await ReadCropped(i, token), videoSettings with { Frame = (uint)i, Crop = null },
                "manual-crop", source.Info, ReadCropped, token);
            expected.Add(manual.Frame.Pixels);
        }
        var result = await VideoExporter.ExportAsync(source.Info.Path, Path.Combine(directory, "cropped-woven.mp4"), videoSettings,
            cancellation: token, overwrite: true, frameObserver: (i, frame) =>
            {
                if (!Same(expected[i], frame.Pixels)) throw new InvalidDataException("Export differs from pre-cropped video at frame " + i);
            });
        var rect = crop.Rectangle(source.Info.Width, source.Info.Height);
        check(result.Width == rect.Width && result.Height == rect.Height && result.Frames == expected.Count,
            "MP4 uses the cropped geometry and crops genuine previous fields before processing");
    }
}
