// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class AnalogSmoke
{
    public static async Task RunAsync(string directory, VideoSource source, Action<bool, string> check, CancellationToken token)
    {
        static bool SamePixels(byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b);
        var catalog = AnalogCatalog.Current;
        check(catalog.Parameters.Length == 30 && catalog.Tapes.Length == 8 && catalog.Decks.Length == 5,
            "Upstream catalog exposes all 30 controls and thirteen VCR conditions");
        var values = catalog.Defaults();
        foreach (var (key, value) in catalog.Tapes.Single(c => c.Name == "Moldy").Values) values[key] = value;
        foreach (var (key, value) in catalog.Decks.Single(c => c.Name == "Dirty heads").Values) values[key] = value;
        values["signal_strength"] = 0.45;
        values["vertical_hold"] = 0.7;
        var analog = new AnalogSettings(true, true, values);
        var shader = Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp");
        var ntsc = NtscProcessor.Defaults();
        var settings = new RenderSettings(false, true, ntsc.ToJsonString(), shader, 160, 2, 0, [],
            new(144, 96), DigitalCatalog.Profile);
        using var pipeline = new EffectPipeline();
        async Task<byte[]> Render(int index, AnalogSettings? effects)
        {
            var sourceFrame = await source.ReadFrameAsync(index, token);
            var frame = await pipeline.RenderAsync(sourceFrame, settings with { Frame = (uint)index, Analog = effects },
                "vcr-test", source.Info, source.ReadFrameAsync, token);
            return frame.Frame.Pixels;
        }

        var dry = await Render(0, null);
        var vcr = await Render(0, analog with { Reception = false });
        var reception = await Render(0, analog with { Vcr = false });
        var both = await Render(0, analog);
        check(!SamePixels(dry, vcr) && !SamePixels(dry, reception) && !SamePixels(vcr, both) && !SamePixels(reception, both),
            "VCR and TV reception visibly affect the pipeline independently and together");
        check(SamePixels(dry, await Render(0, analog with { Vcr = false, Reception = false })),
            "Switching off both stages restores the existing picture");
        var changedReception = analog.Snapshot() with { Reception = false };
        changedReception.Values["signal_strength"] = 0.1;
        check(SamePixels(vcr, await Render(0, changedReception)), "VCR alone ignores disabled reception controls");
        var changedTape = analog.Snapshot() with { Vcr = false };
        changedTape.Values["tape_seed"] = 100;
        check(SamePixels(reception, await Render(0, changedTape)), "Reception alone ignores disabled VCR controls");

        var expected = new List<byte[]>();
        for (var i = 0; i < source.Info.FrameCount; i++) expected.Add(await Render(i, analog));
        check(SamePixels(expected[3], await Render(3, analog)) && SamePixels(expected[9], await Render(9, analog)),
            "VCR and reception reconstruct sequential frames after backward and forward seeking");
        check(SamePixels(expected[0], await Render(0, analog)) && SamePixels(expected[9], await Render(9, analog)),
            "VCR and reception restart deterministically when looping");

        var combined = settings with { Analog = analog, Ntsc = true, Digital = DigitalCatalog.Find("minidv").Values };
        expected.Clear();
        for (var i = 0; i < source.Info.FrameCount; i++)
        {
            var sourceFrame = await source.ReadFrameAsync(i, token);
            var frame = await pipeline.RenderAsync(sourceFrame, combined with { Frame = (uint)i }, "vcr-full",
                source.Info, source.ReadFrameAsync, token);
            expected.Add(frame.Frame.Pixels);
        }
        var output = Path.Combine(directory, "vcr-and-reception.mp4");
        var equal = true;
        var exported = await VideoExporter.ExportAsync(source.Info.Path, output, combined, cancellation: token, overwrite: true,
            frameObserver: (i, frame) => equal &= SamePixels(frame.Pixels, expected[i]));
        check(equal && exported.Frames == expected.Count,
            "Digital video, NTSC, VCR, TV reception and CRT match the full export frame by frame");
        var audio = await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-select_streams", "a:0", "-show_entries",
            "stream=codec_name", "-of", "csv=p=0", output], token);
        check(audio.Trim() == "aac", "Combined VCR and reception export retains audio");

        var preset = new LookPreset("tape-and-tube", 3, true, true, "crt-easymode", 320, 2,
            ntsc, [], Renderer: DigitalCatalog.Profile, Digital: combined.Digital, Analog: analog);
        var loaded = LookPreset.Parse(JsonSerializer.Serialize(preset), ["crt-easymode"]);
        check(loaded.Analog is { Vcr: true, Reception: true } && values.OrderBy(p => p.Key).SequenceEqual(loaded.Analog.Values.OrderBy(p => p.Key)),
            "Preset format 3 round-trips both switches and all stage parameters");
        try
        {
            LookPreset.Parse(JsonSerializer.Serialize(preset with { Version = 2 }), ["crt-easymode"]);
            throw new InvalidOperationException("Version 2 unexpectedly accepted the new VCR settings.");
        }
        catch (InvalidDataException) { check(true, "Older formats reject VCR settings explicitly"); }
    }
}
