// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class ExportSmoke
{
    private sealed class InlineProgress(Action<ExportProgress> action) : IProgress<ExportProgress>
    { public void Report(ExportProgress value) => action(value); }

    public static async Task RunAsync()
    {
        var directory = Path.Combine(Paths.Root, "artifacts", "export-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "export-smoke-error.txt"), "");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var token = timeout.Token;
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }
        var sourcePath = Path.Combine(directory, "Original mit Ton ä.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-copyts",
            "-f", "lavfi", "-i", "testsrc2=size=96x64:rate=6:duration=2",
            "-itsoffset", "0.4", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=48000:duration=0.7",
            "-c:v", "mpeg4", "-q:v", "2", "-c:a", "aac", "-avoid_negative_ts", "disabled", sourcePath], token);
        var shiftedPath = Path.Combine(directory, "Original mit Zeitversatz.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-copyts", "-itsoffset", "1",
            "-i", sourcePath, "-c", "copy", "-avoid_negative_ts", "disabled", shiftedPath], token);
        var outputPath = Path.Combine(directory, "Effekt mit Ton.mp4");
        var settings = new RenderSettings(true, true, NtscProcessor.Defaults().ToJsonString(),
            Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp"), 320, 3, 0, []);
        var progress = new List<ExportProgress>();
        var result = await VideoExporter.ExportAsync(shiftedPath, outputPath, settings, new InlineProgress(progress.Add), token, overwrite: true);
        using var metadata = await Probe(outputPath, token);
        var streams = metadata.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        var video = streams.Single(s => s.GetProperty("codec_type").GetString() == "video");
        var audio = streams.Single(s => s.GetProperty("codec_type").GetString() == "audio");
        Check(video.GetProperty("codec_name").GetString() == "h264", "H.264-MP4 erzeugt");
        Check(audio.GetProperty("codec_name").GetString() == "aac", "Originalton als AAC übernommen");
        Check(int.Parse(video.GetProperty("nb_read_frames").GetString()!) == 12, "Alle 12 Bilder vollständig exportiert");
        Check(video.GetProperty("avg_frame_rate").GetString() == "6/1", "Bildrate erhalten");
        Check(video.GetProperty("color_space").GetString() == "bt709", "Farbumsetzung und Kennzeichnung für SDR-Video");
        Check(result.Width == 288 && result.Height == 192, "Effekt-Ausgabeauflösung");
        static double Seconds(JsonElement stream, string name) => double.Parse(stream.GetProperty(name).ToString(), System.Globalization.CultureInfo.InvariantCulture);
        Check(Math.Abs(Seconds(video, "start_time")) < 0.01, "Video-Zeitstempel beginnen bei null");
        Check(Math.Abs(Seconds(audio, "start_time")) < 0.03, "Ton beginnt mit passendem Zeitraster");
        Check(Math.Abs(Seconds(video, "duration") - 2) < 0.01, "Videolänge erhalten");
        Check(Math.Abs(Seconds(audio, "duration") - 2) < 0.05, "Kurzer Ton kürzt das Video nicht");
        Check(progress.Count(p => !p.Finalizing && p.CompletedFrames > 0) == 12 && progress.Last().Finalizing,
            "Fortschritt für jedes Bild und Abschluss gemeldet");

        // Verify the audible delay, not merely the stream's container timestamps.
        using (var decodeAudio = MediaTools.Start("ffmpeg", ["-v", "error", "-nostdin", "-i", outputPath,
            "-map", "0:a:0", "-ac", "1", "-ar", "48000", "-f", "f32le", "pipe:1"]))
        {
            var errors = decodeAudio.StandardError.ReadToEndAsync();
            using var memory = new MemoryStream();
            await decodeAudio.StandardOutput.BaseStream.CopyToAsync(memory, token);
            await decodeAudio.WaitForExitAsync(token);
            if (decodeAudio.ExitCode != 0) throw new InvalidOperationException(await errors);
            var bytes = memory.ToArray();
            double Energy(double from, double to)
            {
                double sum = 0; var start = (int)(from * 48000); var end = Math.Min((int)(to * 48000), bytes.Length / 4);
                for (var sample = start; sample < end; sample++) { var value = BitConverter.ToSingle(bytes, sample * 4); sum += value * value; }
                return sum / Math.Max(1, end - start);
            }
            Check(Energy(0.03, 0.2) < 0.000001 && Energy(0.55, 0.75) > 0.001,
                "Verzögert einsetzender Originalton bleibt auch bei verschobenem Quell-Zeitstempel synchron");
        }

        await using (var original = await VideoSource.OpenAsync(shiftedPath, token))
        await using (var exported = await VideoSource.OpenAsync(outputPath, token))
        {
            Check(original.Info.StartTime > 0.9, "Testquelle besitzt einen echten Zeitversatz");
            using var pipeline = new EffectPipeline();
            PixelFrame? expected = null;
            for (int i = 0; i <= 3; i++) expected = pipeline.Render(await original.ReadFrameAsync(i, token), settings with { Frame = (uint)i }).Frame;
            var actual = await exported.ReadFrameAsync(3, token);
            double error = 0;
            for (int i = 0; i < actual.Pixels.Length; i++) if (i % 4 != 3) error += Math.Abs(actual.Pixels[i] - expected!.Pixels[i]);
            Check(error / (actual.Width * actual.Height * 3) < 18, "Export enthält die berechneten NTSC-/CRT-Effekte");
        }

        var silentPath = Path.Combine(directory, "ohne-ton.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-i", sourcePath, "-an", "-c:v", "copy", silentPath], token);
        var silentOutput = Path.Combine(directory, "ohne-ton-export.mp4");
        await VideoExporter.ExportAsync(silentPath, silentOutput, settings with { Ntsc = false, Crt = false }, cancellation: token, overwrite: true);
        using (var silent = await Probe(silentOutput, token))
            Check(silent.RootElement.GetProperty("streams").EnumerateArray().All(s => s.GetProperty("codec_type").GetString() != "audio"), "Videos ohne Ton exportierbar");

        var oddPath = Path.Combine(directory, "ungerade.mkv");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc=size=81x61:rate=2:duration=1",
            "-c:v", "ffv1", "-pix_fmt", "bgr0", oddPath], token);
        var odd = await VideoExporter.ExportAsync(oddPath, Path.Combine(directory, "ungerade-export.mp4"), settings with { Ntsc = false, Crt = false }, cancellation: token, overwrite: true);
        Check(odd.Width == 82 && odd.Height == 62, "Ungerade Bildmaße für H.264 verlustfrei am Rand ergänzt");

        var fractionalPath = Path.Combine(directory, "gebrochene-bildrate.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i", "testsrc2=size=96x64:rate=30000/1001:duration=0.5",
            "-c:v", "mpeg4", fractionalPath], token);
        var fractionalOutput = Path.Combine(directory, "gebrochene-bildrate-export.mp4");
        await VideoExporter.ExportAsync(fractionalPath, fractionalOutput, settings with { Ntsc = false, Crt = false }, cancellation: token, overwrite: true);
        using (var fractional = await Probe(fractionalOutput, token))
        {
            var stream = fractional.RootElement.GetProperty("streams")[0];
            Check(stream.GetProperty("avg_frame_rate").GetString() == "30000/1001" && stream.GetProperty("nb_read_frames").GetString() == "15",
                "Gebrochene Bildrate ohne Bildverlust exportiert");
        }
        var variablePath = Path.Combine(directory, "variable-bildrate.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-i", fractionalPath, "-vf", "select=not(mod(n\\,2))+not(mod(n\\,5))",
            "-fps_mode", "vfr", "-c:v", "mpeg4", variablePath], token);
        var variableOutput = Path.Combine(directory, "variable-bildrate-export.mp4");
        var variableResult = await VideoExporter.ExportAsync(variablePath, variableOutput, settings with { Ntsc = false, Crt = false }, cancellation: token, overwrite: true);
        using (var variable = await Probe(variableOutput, token))
        {
            var stream = variable.RootElement.GetProperty("streams")[0];
            Check(int.Parse(stream.GetProperty("nb_read_frames").GetString()!) == variableResult.Frames,
                "Variable Bildrate vollständig auf dem Vorschau-Zeitraster exportiert");
        }

        var protectedPath = Path.Combine(directory, "bestehendes-ziel.mp4");
        var sentinel = new byte[] { 9, 8, 7, 6 };
        File.WriteAllBytes(protectedPath, sentinel);
        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var rejected = false;
            try
            {
                await VideoExporter.ExportAsync(sourcePath, protectedPath, settings,
                    new InlineProgress(p => { if (p.CompletedFrames == 2) cancelled.Cancel(); }), cancelled.Token, overwrite: true);
            }
            catch (OperationCanceledException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(protectedPath).SequenceEqual(sentinel), "Abbruch schützt eine bestehende Zieldatei");
        }
        Check(Directory.GetFiles(directory, ".ntscrt-*.mp4").Length == 0, "Temporäre Dateien nach Abbruch entfernt");
        using (var cancelled = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            var rejected = false;
            try
            {
                await VideoExporter.ExportAsync(sourcePath, protectedPath, settings,
                    new InlineProgress(p => { if (p.Finalizing) cancelled.Cancel(); }), cancelled.Token, overwrite: true);
            }
            catch (OperationCanceledException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(protectedPath).SequenceEqual(sentinel), "Abbruch beim Abschluss schützt das Ziel");
        }
        var samePathRejected = false;
        try { await VideoExporter.ExportAsync(sourcePath, sourcePath, settings, cancellation: token, overwrite: true); }
        catch (InvalidOperationException) { samePathRejected = true; }
        Check(samePathRejected, "Quellvideo kann nicht überschrieben werden");
        var overwriteRejected = false;
        try { await VideoExporter.ExportAsync(sourcePath, protectedPath, settings, cancellation: token); }
        catch (IOException) { overwriteRejected = true; }
        Check(overwriteRejected && File.ReadAllBytes(protectedPath).SequenceEqual(sentinel), "Vorhandene Ziele benötigen ausdrückliche Überschreibfreigabe");
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "export-smoke.json"), JsonSerializer.Serialize(new
        { passed = true, count = checks.Count, checks, outputPath, result }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<JsonDocument> Probe(string path, CancellationToken cancellation) => JsonDocument.Parse(
        await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-count_frames", "-show_streams", "-of", "json", path], cancellation));
}
