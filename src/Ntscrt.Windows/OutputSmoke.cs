// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class OutputSmoke
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Paths.Root, "artifacts", "output-fixtures"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "output-smoke-error.txt"), "");
        var checks = new List<string>();
        checks.AddRange(ComparisonSmoke.Run());
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        static PixelFrame Solid(int w, int h)
        {
            var pixels = new byte[w * h * 4];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = 255; pixels[i + 3] = 255; }
            return new(w, h, pixels);
        }
        static bool Red(PixelFrame p, int x, int y) => p.Pixels[(y * p.Width + x) * 4 + 2] == 255;
        var fit = OutputComposer.Compose(Solid(4, 3), new OutputSettings(8, 8).Geometry(4, 3));
        Check(!Red(fit, 4, 0) && Red(fit, 4, 1) && Red(fit, 4, 6) && !Red(fit, 4, 7), "Einpassen erhält das Seitenverhältnis und ergänzt schwarze Ränder");
        var portrait = OutputComposer.Compose(Solid(2, 4), new OutputSettings(8, 4).Geometry(2, 4));
        Check(!Red(portrait, 2, 2) && Red(portrait, 3, 2) && Red(portrait, 4, 2) && !Red(portrait, 5, 2), "Hochkantbilder werden mit seitlichen Rändern eingepasst");
        var stripeBytes = new byte[6 * 2 * 4];
        for (int y = 0; y < 2; y++) for (int x = 0; x < 6; x++) { stripeBytes[(y * 6 + x) * 4] = (byte)(x * 40); stripeBytes[(y * 6 + x) * 4 + 3] = 255; }
        var crop = OutputComposer.Compose(new(6, 2, stripeBytes), new OutputSettings(2, 2, OutputFit.Crop).Geometry(6, 2));
        Check(crop.Pixels[0] == 80 && crop.Pixels[4] == 120, "Beschnitt verwendet die Bildmitte ohne Verzerrung");
        var checker = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        { var i = (y * 16 + x) * 4; checker[i] = checker[i + 1] = checker[i + 2] = (byte)((x + y) % 2 * 255); checker[i + 3] = 255; }
        var down = OutputComposer.Compose(new(16, 16, checker), new OutputSettings(1, 1).Geometry(16, 16));
        Check(down.Pixels[0] == 128 && down.Pixels[1] == 128 && down.Pixels[2] == 128, "Verkleinerung mittelt feine Muster gegen Aliasing");
        var rejected = 0;
        foreach (var invalid in new[] { new OutputSettings(0, 1080), new(-1, 1080), new(8193, 1), new(8192, 8192), new(100, 100, (OutputFit)99) })
            try { invalid.Validate(); } catch (ArgumentException) { rejected++; }
        Check(rejected == 5, "Ungültige oder übergroße Zielmaße werden abgelehnt");
        var custom = new OutputSettings(1080, 1920, OutputFit.Crop);
        Check(JsonSerializer.Deserialize<OutputSettings>(JsonSerializer.Serialize(custom)) == custom, "Ausgabegröße und Bildanpassung überstehen einen JSON-Roundtrip");

        var source = PixelFrame.TestPattern();
        var settings = new RenderSettings(false, true, NtscProcessor.Defaults().ToJsonString(),
            Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp"), 320, 2, 0, [], new(512, 288));
        using var pipeline = new EffectPipeline();
        var first = pipeline.Render(source, settings).Frame;
        var otherScale = pipeline.Render(source, settings with { Scale = 4 }).Frame;
        Check(first.Width == 512 && first.Height == 288 && first.Pixels.SequenceEqual(otherScale.Pixels), "Feste Ausgabegröße ist unabhängig vom bisherigen CRT-Skalierungsfaktor");
        using (var crt = new CrtProcessor())
        {
            crt.Load(settings.Shader);
            var reference = OutputComposer.Compose(crt.Render(source, 384, 288, 0, settings.ShaderValues), settings.Output!.Geometry(source.Width, source.Height));
            Check(reference.Pixels.SequenceEqual(first.Pixels), "CRT-Maske wird direkt in der Zielauflösung berechnet");
        }
        foreach (var output in new[] { new OutputSettings(1280, 720), new(1920, 1080), new(3840, 2160) })
        {
            var frame = pipeline.Render(source, settings with { Output = output }).Frame;
            var path = Path.Combine(directory, $"crt-{output.Width}x{output.Height}.png"); frame.Save(path);
            var loaded = PixelFrame.Read(path);
            Check(loaded.Width == output.Width && loaded.Height == output.Height && loaded.Pixels.SequenceEqual(frame.Pixels), $"PNG in {output.Width} × {output.Height} mit unveränderten Pixeln");
        }
        var cropped = pipeline.Render(source, settings with { Output = new(640, 360, OutputFit.Crop) }).Frame;
        Check(cropped.Width == 640 && cropped.Height == 360, "CRT-Ausgabe mit Beschnitt hat exakte Zielmaße");
        var plain = pipeline.Render(source, settings with { Crt = false, Output = new(640, 360) }).Frame;
        Check(plain.Width == 640 && plain.Height == 360, "Ausgabegröße funktioniert auch ohne CRT-Effekt");
        var automatic = pipeline.Render(source, settings with { Output = null }).Frame;
        Check(automatic.Width == 640 && automatic.Height == 480, "Automatische Ausgabe bleibt mit bisherigen Presets kompatibel");

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = timeout.Token;
        var videoPath = Path.Combine(directory, "source.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=size=96x64:rate=3:duration=1",
            "-c:v", "mpeg4", videoPath], token);
        foreach (var output in new[] { new OutputSettings(320, 180), new(321, 181), new(180, 320, OutputFit.Crop) })
        {
            var path = Path.Combine(directory, $"output-{output.Width}x{output.Height}.mp4");
            var result = await VideoExporter.ExportAsync(videoPath, path, settings with { Output = output }, cancellation: token, overwrite: true);
            await using var video = await VideoSource.OpenAsync(path, token);
            Check(video.Info.Width == ((output.Width + 1) & ~1) && video.Info.Height == ((output.Height + 1) & ~1) && result.Frames == 3,
                $"MP4 mit Zielmaß {output.Width} × {output.Height}, geraden Kodiermaßen und vollständiger Bildanzahl");
            var pngFrame = pipeline.Render(source, settings with { Output = output }).Frame;
            Check(pngFrame.Width == output.Width && pngFrame.Height == output.Height, $"PNG behält auch eigene Maße {output.Width} × {output.Height} exakt bei");
        }
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "output-smoke.json"), JsonSerializer.Serialize(new
        { passed = true, count = checks.Count, checks, directory }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
