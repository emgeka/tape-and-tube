// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class CrtTemporalDiagnostic
{
    public static async Task RunClipAsync(string sourcePath, string presetPath)
    {
        var directory = Path.Combine(Paths.Root, "artifacts", "export-investigation", "local-reproduction");
        Directory.CreateDirectory(directory);
        using var preset = JsonDocument.Parse(File.ReadAllText(presetPath));
        var p = preset.RootElement;
        var values = p.GetProperty("Parameters").Deserialize<Dictionary<string, float>>()!;
        var settings = new RenderSettings(p.GetProperty("NtscEnabled").GetBoolean(), p.GetProperty("CrtEnabled").GetBoolean(),
            p.GetProperty("Ntsc").GetRawText(), Path.Combine(Paths.ShaderRoot, "crt", p.GetProperty("Shader").GetString()! + ".slangp"),
            p.GetProperty("Width").GetInt32(), p.GetProperty("Scale").GetInt32(), 0, values,
            p.TryGetProperty("Output", out var output) && output.ValueKind != JsonValueKind.Null ? output.Deserialize<OutputSettings>() : null);
        using var trace = new StreamWriter(Path.Combine(directory, "raw-frame-stats.jsonl")) { AutoFlush = true };
        var timer = System.Diagnostics.Stopwatch.StartNew();
        var result = await VideoExporter.ExportAsync(sourcePath, Path.Combine(directory, "local-export.mp4"), settings,
            overwrite: true, frameObserver: (index, frame) =>
            {
                long sum = 0; int maximum = 0, count = 0;
                for (int pixel = 0; pixel < frame.Pixels.Length; pixel += 4 * 97)
                    for (int channel = 0; channel < 3; channel++) { int value = frame.Pixels[pixel + channel]; sum += value; maximum = Math.Max(maximum, value); count++; }
                trace.WriteLine(JsonSerializer.Serialize(new { frame = index, mean = (double)sum / count, maximum, elapsed = timer.Elapsed.TotalSeconds }));
                if (index is 0 or 480 or 481 or 526 or 527 or 600 or 743) frame.Save(Path.Combine(directory, $"raw-{index}.png"));
            });
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task RunAsync()
    {
        var directory = Path.Combine(Paths.Root, "artifacts", "crt-temporal-diagnostic");
        Directory.CreateDirectory(directory);
        const int width = 96, height = 171;
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int p = (y * width + x) * 4;
            pixels[p] = (byte)(x * 255 / (width - 1));
            pixels[p + 1] = (byte)(y * 255 / (height - 1));
            pixels[p + 2] = (byte)(((x / 8 + y / 8) % 2) * 255);
            pixels[p + 3] = 255;
        }
        var source = new PixelFrame(width, height, pixels);
        var results = new List<object>();
        var defaults = NtscProcessor.Defaults().ToJsonString();
        foreach (bool ntsc in new[] { false, true })
        foreach (int animation in new[] { 0, 1 })
        {
            using var pipeline = new EffectPipeline();
            var settings = new RenderSettings(ntsc, true, defaults,
                Path.Combine(Paths.ShaderRoot, "crt", "crtsim.slangp"), 256, 2, 0,
                new() { ["animate_artifacts"] = animation }, new(960, 1707));
            // Preserve sequential feedback for startup; sample later frame indices
            // separately to distinguish frame phase from elapsed render time.
            foreach (uint frame in new uint[] { 0, 1, 2, 3, 4, 5, 480, 481 })
            {
                var rendered = pipeline.Render(source, settings with { Frame = frame });
                long sum = 0; int maximum = 0;
                for (int i = 0; i < rendered.Frame.Pixels.Length; i++)
                    if (i % 4 != 3) { int v = rendered.Frame.Pixels[i]; sum += v; maximum = Math.Max(maximum, v); }
                double mean = (double)sum / (rendered.Frame.Width * rendered.Frame.Height * 3);
                var name = $"ntsc-{ntsc}-animation-{animation}-frame-{frame}.png";
                if (frame is 0 or 1 or 480) rendered.Frame.Save(Path.Combine(directory, name));
                results.Add(new { ntsc, animation, frame, mean, maximum, parameters = frame == 0 ? rendered.Parameters : null });
            }
        }
        File.WriteAllText(Path.Combine(directory, "report.json"), JsonSerializer.Serialize(new
        {
            shader = "crtsim", width = 960, height = 1707,
            note = "Diagnostic samples, not a reproduction of the tester clip or a continuous 16-second export.", results
        }, new JsonSerializerOptions { WriteIndented = true }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var token = timeout.Token;
        var videoSource = Path.Combine(directory, "synthetic-portrait.mkv");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=96x172:rate=24:duration=20", "-c:v", "ffv1", videoSource], token);
        var exportSettings = new RenderSettings(true, true, defaults,
            Path.Combine(Paths.ShaderRoot, "crt", "crtsim.slangp"), 256, 2, 0,
            new() { ["animate_artifacts"] = 0 }, new(960, 1707));
        var exportPath = Path.Combine(directory, "synthetic-crtsim-animation-off.mp4");
        var exportResult = await VideoExporter.ExportAsync(videoSource, exportPath, exportSettings,
            cancellation: token, overwrite: true);
        await using var decoded = await VideoSource.OpenAsync(exportPath, token);
        var samples = new List<object>();
        foreach (int frameIndex in new[] { 0, 24, 360, 383, 384, 385, 408, 432, 456, 479 })
        {
            var frame = await decoded.ReadFrameAsync(frameIndex, token);
            long sum = 0; int maximum = 0;
            for (int i = 0; i < frame.Pixels.Length; i++)
                if (i % 4 != 3) { int v = frame.Pixels[i]; sum += v; maximum = Math.Max(maximum, v); }
            samples.Add(new { frame = frameIndex, seconds = frameIndex / 24.0,
                mean = (double)sum / (frame.Width * frame.Height * 3), maximum });
        }
        File.WriteAllText(Path.Combine(directory, "export-report.json"), JsonSerializer.Serialize(new
        {
            exportResult, samples,
            note = "Continuous synthetic 20-second CRTSim export with default NTSC and animation disabled; not the original tester clip."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
