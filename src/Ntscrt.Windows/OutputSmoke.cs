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
        var snappedFit = new OutputSettings(150, 150).ScanlineGeometry(96, 64, 64, OutputSettings.MaxSide);
        Check(snappedFit is { Width: 192, Height: 192, ContentHeight: 128, Top: 32 },
            "Scanline-Raster richtet die Bildhöhe innerhalb der schwarzen Ränder aus");
        var snappedPortrait = new OutputSettings(960, 1707, OutputFit.Crop).ScanlineGeometry(540, 960, 320, OutputSettings.MaxSide);
        Check(snappedPortrait is { Width: 900, Height: 1600, ContentHeight: 1600 },
            "Scanline-Raster erhält einen Hochkant-Beschnitt");
        var gifLimit = new OutputSettings(480, 270).ScanlineGeometry(1920, 1080, 1080, VideoExporter.GifMaxAutomaticSide);
        Check(gifLimit is { Width: 480, Height: 270 }, "Automatische GIF-Größe hält ihre Grenze bei aktivem Raster ein");
        var gifInfo = new VideoInfo("", 1920, 1080, 30, "30/1", 150, 0);
        var smallGif = VideoExporter.EstimateGif(gifInfo, null, null, new(), false, 0);
        Check(smallGif is { Width: 480, Height: 270, Frames: 63, IsLarge: false }, "GIF-Schätzung übernimmt automatische Maße und Bildrate");
        var bigGif = VideoExporter.EstimateGif(gifInfo, new(320, 200), new SourceCrop("1:1"), new(800, 30), false, 0);
        Check(bigGif is { Width: 800, Height: 800, Frames: 150, IsLarge: true } && Math.Abs(bigGif.Megabytes - 76.8) < 0.001,
            "GIF-Schätzung berücksichtigt Beschnitt und eigene Einstellungen sowie die 10-MB-Schwelle");
        var snappedGif = VideoExporter.EstimateGif(gifInfo, null, null, new(800, 20), true, 240);
        Check(snappedGif is { Width: 853, Height: 480, Frames: 100 }, "Die GIF-Schätzung berücksichtigt das Scanline-Raster");

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
        var tallSource = Solid(1904, 2544);
        var aperture = (await pipeline.RenderAsync(tallSource, settings with
        {
            Renderer = DigitalCatalog.Profile,
            Shader = Path.Combine(Paths.ShaderRoot, "crt", "crt-aperture.slangp"),
            DownscaleEnabled = false,
            Output = new(tallSource.Width, tallSource.Height)
        }, "aperture-without-downscale", cancellation: token)).Frame;
        long red = 0;
        for (var i = 2; i < aperture.Pixels.Length; i += 4) red += aperture.Pixels[i];
        Check(aperture.Width == tallSource.Width && aperture.Height == tallSource.Height &&
            red > 20L * aperture.Width * aperture.Height,
            "CRT Aperture zeigt bei 1904 × 2544 auch ohne interne Verkleinerung ein Bild");
        var image = pipeline.Render(source, settings with { Output = new(321, 181, OutputFit.Crop) }).Frame;
        async Task<PixelFrame> WriteAndRead(string extension, int quality = 90, bool losslessWebp = true, string name = "export-image")
        {
            var path = Path.Combine(directory, name + extension);
            await Task.Run(() => ImageExporter.SaveAsync(image, path, quality, losslessWebp, token));
            if (extension != ".webp") return PixelFrame.Read(path);
            var decoded = Path.ChangeExtension(path, ".decoded.png");
            await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-y", "-i", path, "-frames:v", "1", "-update", "1", decoded], token);
            return PixelFrame.Read(decoded);
        }
        foreach (var extension in new[] { ".png", ".bmp", ".tif", ".tiff", ".webp" })
        {
            var saved = await WriteAndRead(extension);
            Check(saved.Width == image.Width && saved.Height == image.Height && saved.Pixels.SequenceEqual(image.Pixels),
                $"{extension} round-trips the cropped CRT frame at odd dimensions without changing pixels");
        }
        foreach (var extension in new[] { ".jpg", ".jpeg", ".webp" })
        {
            var high = await WriteAndRead(extension, quality: 90, losslessWebp: false, name: "high-quality");
            var low = await WriteAndRead(extension, quality: 15, losslessWebp: false, name: "low-quality");
            Check(high.Width == image.Width && high.Height == image.Height &&
                low.Width == high.Width && low.Height == high.Height && !high.Pixels.SequenceEqual(low.Pixels),
                $"{extension} keeps image dimensions and applies the selected lossy quality");
        }
        var protectedImage = Path.Combine(directory, "protected-image.png");
        var sentinel = new byte[] { 1, 2, 3 };
        File.WriteAllBytes(protectedImage, sentinel);
        var imageCanceled = false;
        try { await ImageExporter.SaveAsync(image, protectedImage, 90, true, new CancellationToken(canceled: true)); }
        catch (OperationCanceledException) { imageCanceled = true; }
        Check(imageCanceled && File.ReadAllBytes(protectedImage).SequenceEqual(sentinel), "Image cancellation preserves the existing file");
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
