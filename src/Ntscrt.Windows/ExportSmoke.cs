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

        Check(await AudioAligned(outputPath, token),
            "Verzögert einsetzender Originalton bleibt auch bei verschobenem Quell-Zeitstempel synchron");

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

        foreach (var (format, videoCodec, audioCodec) in new[]
        {
            (VideoExportFormat.ProRes, "prores", "pcm_s24le"),
            (VideoExportFormat.WebM, "vp9", "opus")
        })
        {
            var extension = VideoExporter.Extension(format);
            var target = Path.Combine(directory, $"Effekt mit Ton.{extension}");
            var clip = await VideoExporter.ExportAsync(shiftedPath, target, settings, cancellation: token, overwrite: true, format: format);
            using var probe = await Probe(target, token);
            var encoded = probe.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            var picture = encoded.Single(s => s.GetProperty("codec_type").GetString() == "video");
            var sound = encoded.Single(s => s.GetProperty("codec_type").GetString() == "audio");
            Check(picture.GetProperty("codec_name").GetString() == videoCodec && sound.GetProperty("codec_name").GetString() == audioCodec,
                $"{extension} verwendet {videoCodec} und {audioCodec}");
            Check(picture.GetProperty("nb_read_frames").GetString() == "12" && picture.GetProperty("avg_frame_rate").GetString() == "6/1" &&
                picture.GetProperty("width").GetInt32() == clip.Width && picture.GetProperty("height").GetInt32() == clip.Height &&
                Math.Abs(Seconds(picture, "start_time")) < 0.02, $"{extension} übernimmt Bildanzahl, Bildrate, Auflösung und Startzeit");
            Check(await AudioAligned(target, token), $"{extension} behält den Tonversatz und die Dauer bei");
            var targetSilent = Path.Combine(directory, $"ohne-ton-export.{extension}");
            await VideoExporter.ExportAsync(silentPath, targetSilent, settings with { Ntsc = false, Crt = false },
                cancellation: token, overwrite: true, format: format);
            using var silentProbe = await Probe(targetSilent, token);
            Check(silentProbe.RootElement.GetProperty("streams").GetArrayLength() == 1, $"{extension} exportiert Clips ohne Ton");
        }

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
        Check(Directory.GetFiles(directory, ".tapetube-*.mp4").Length == 0, "Temporäre Dateien nach Abbruch entfernt");
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
        await GifSmoke.RunAsync(directory, shiftedPath, Check, token);
        await CheckStillWithoutKeys(directory, Check, token);
        await CheckHistoricalLooks(directory, shiftedPath, Check, token);
        await CheckBairdLook(directory, Check, token);
        await CheckBritish405Look(directory, Check, token);
        await CheckKinescopeLook(directory, Check, token);
        await CheckHistoricalCombinations(directory, shiftedPath, Check, token);
        await CheckSideBySideExport(directory, shiftedPath, Check, token);
        await CheckPreviewAudio(directory, shiftedPath, Check, token);
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "export-smoke.json"), JsonSerializer.Serialize(new
        { passed = true, count = checks.Count, checks, outputPath, result }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task CheckBairdLook(string directory, Action<bool, string> check, CancellationToken token)
    {
        var preset = BasicLookCatalog.Groups.SelectMany(group => group.Looks)
            .Single(look => look.Display == HistoricalDisplay.Baird30).CreatePreset("crt-easymode");
        check(LookPreset.Parse(JsonSerializer.Serialize(preset), [preset.Shader]).Display == HistoricalDisplay.Baird30 &&
            LookPreset.Parse(JsonSerializer.Serialize(preset with { Display = HistoricalDisplay.None, Baird = true }),
                [preset.Shader]).Display == HistoricalDisplay.Baird30, "New and existing Baird presets load successfully");
        var original = Path.Combine(directory, "baird-portrait.mp4");
        var target = Path.Combine(directory, "basic-baird-30.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc=size=96x168:rate=25:duration=0.48", "-c:v", "libx264", "-pix_fmt", "yuv420p", original], token);
        await using var source = await VideoSource.OpenAsync(original, token);
        using var pipeline = new EffectPipeline();
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], new(192, 280), Display: HistoricalDisplay.Baird30);
        async Task<PixelFrame> Preview(int index) => (await pipeline.RenderHistoricalVideoAsync(
            settings with { Frame = (uint)index }, "baird-preview", source.Info, source.ReadFrameAsync, token)).Frame;
        var afterJump = await Preview(5);
        var held = await Preview(4);
        var next = await Preview(6);
        check(afterJump.Pixels.SequenceEqual(held.Pixels) && !next.Pixels.SequenceEqual(held.Pixels) &&
            EffectPipeline.HistoricalSampleIndex(settings.Display, 299, 30000.0 / 1001) == 298,
            "Baird seeks to the same held image on the 12.5 Hz clock, including with a fractional source rate");
        var still = pipeline.Render(await source.ReadFrameAsync(4, token), settings).Frame;
        still.Save(Path.Combine(directory, "basic-baird-30.png"));
        var center = (still.Height / 2 * still.Width + still.Width / 2) * 4;
        check(still is { Width: 192, Height: 280 } && still.Pixels[0] == 0 && still.Pixels[3] == 255 &&
            still.Pixels[center + 2] > still.Pixels[center + 1] && still.Pixels[center + 1] > still.Pixels[center],
            "The Baird still preserves output dimensions, centers its portrait aperture and has amber luminance");
        var matches = false;
        var exported = await VideoExporter.ExportAsync(original, target, settings, cancellation: token, overwrite: true,
            frameObserver: (i, frame) => { if (i == 5) matches = frame.Pixels.SequenceEqual(afterJump.Pixels); });
        check(matches && exported.Width == still.Width && exported.Height == still.Height &&
            exported.Frames == source.Info.FrameCount, "Baird video export matches the seeked preview without changing the timeline");
    }

    private static async Task CheckBritish405Look(string directory, Action<bool, string> check, CancellationToken token)
    {
        var preset = BasicLookCatalog.Groups.SelectMany(group => group.Looks)
            .Single(look => look.Display == HistoricalDisplay.British405).CreatePreset("crt-easymode");
        check(LookPreset.Parse(JsonSerializer.Serialize(preset), [preset.Shader]).Display == HistoricalDisplay.British405,
            "The British 405-line display survives a preset round trip");
        var original = Path.Combine(directory, "british-405-source.mp4");
        var target = Path.Combine(directory, "basic-british-405.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=320x240:rate=50:duration=0.32", "-c:v", "libx264", "-pix_fmt", "yuv420p", original], token);
        await using var source = await VideoSource.OpenAsync(original, token);
        using var pipeline = new EffectPipeline();
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], new(240, 192), Display: preset.Display);
        var still = pipeline.Render(await source.ReadFrameAsync(4, token), settings with { Output = new(1120, 800) }).Frame;
        still.Save(Path.Combine(directory, "basic-british-405.png"));
        int Offset(int x, int y) => (y * still.Width + x) * 4;
        var center = Offset(560, 400);
        var corner = Offset(61, 1);
        var padding = Offset(50, 400);
        check(still is { Width: 1120, Height: 800 } && still.Pixels[center] > 20 &&
            still.Pixels[center] == still.Pixels[center + 1] && still.Pixels[center] == still.Pixels[center + 2] &&
            still.Pixels[padding] == 0 && still.Pixels[corner] == 0 && still.Pixels[padding + 3] == 255,
            "The 405-line still has a monochrome 5:4 aperture with rounded corners inside the requested canvas");
        async Task<PixelFrame> Preview(int index) => (await pipeline.RenderHistoricalVideoAsync(
            settings with { Frame = (uint)index }, "405-preview", source.Info, source.ReadFrameAsync, token)).Frame;
        var afterJump = await Preview(5);
        var held = await Preview(4);
        var next = await Preview(6);
        check(afterJump.Pixels.SequenceEqual(held.Pixels) && !next.Pixels.SequenceEqual(held.Pixels),
            "The 405-line preview holds a 25 Hz picture from a 50 fps source, including after a seek");
        var matches = false;
        var exported = await VideoExporter.ExportAsync(original, target, settings, cancellation: token, overwrite: true,
            frameObserver: (i, frame) => { if (i == 5) matches = frame.Pixels.SequenceEqual(afterJump.Pixels); });
        check(matches && exported.Frames == source.Info.FrameCount && exported.Width == 240 && exported.Height == 192,
            "The 405-line MP4 export matches the preview and preserves the source timeline");
    }

    private static async Task CheckKinescopeLook(string directory, Action<bool, string> check, CancellationToken token)
    {
        var preset = BasicLookCatalog.Groups.SelectMany(group => group.Looks)
            .Single(look => look.Film).CreatePreset("crt-easymode");
        check(LookPreset.Parse(JsonSerializer.Serialize(preset), [preset.Shader]).Film &&
            LookPreset.Parse(JsonSerializer.Serialize(preset with { Film = false, Display = HistoricalDisplay.Kinescope }),
                [preset.Shader]) is { Film: true, Display: HistoricalDisplay.None },
            "New and existing Kinescope presets restore the film stage");
        var original = Path.Combine(directory, "kinescope-source.mp4");
        var target = Path.Combine(directory, "basic-kinescope.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=320x240:rate=48:duration=0.5", "-f", "lavfi", "-i", "sine=duration=0.5",
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", original], token);
        await using var source = await VideoSource.OpenAsync(original, token);
        using var pipeline = new EffectPipeline();
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], new(320, 240), Film: preset.Film);
        var originalFrame = await source.ReadFrameAsync(4, token);
        var stillSettings = settings with { Output = new(640, 432) };
        var still = pipeline.Render(originalFrame, stillSettings).Frame;
        still.Save(Path.Combine(directory, "basic-kinescope.png"));
        var center = (still.Height / 2 * still.Width + still.Width / 2) * 4;
        check(still is { Width: 640, Height: 432 } && still.Pixels[center] > 15 &&
            still.Pixels[center] == still.Pixels[center + 1] && still.Pixels[center] == still.Pixels[center + 2] &&
            still.Pixels[0] == 0 && still.Pixels.SequenceEqual(pipeline.Render(originalFrame, stillSettings).Frame.Pixels),
            "The Kinescope still has a monochrome 4:3 picture within the canvas and repeatable film grain");

        Func<int, CancellationToken, Task<PixelFrame>> fixedSource = (_, _) => Task.FromResult(originalFrame);
        var first = await pipeline.RenderHistoricalVideoAsync(settings with { Frame = 4 }, "kinescope-fixed", source.Info, fixedSource, token);
        var second = await pipeline.RenderHistoricalVideoAsync(settings with { Frame = 6 }, "kinescope-fixed", source.Info, fixedSource, token);
        check(!first.Frame.Pixels.SequenceEqual(second.Frame.Pixels), "Kinescope film grain and movement vary across exposures on a static source");

        async Task<PixelFrame> Preview(int index) => (await pipeline.RenderHistoricalVideoAsync(
            settings with { Frame = (uint)index }, "kinescope-preview", source.Info, source.ReadFrameAsync, token)).Frame;
        var afterJump = await Preview(5);
        var held = await Preview(4);
        var next = await Preview(6);
        check(afterJump.Pixels.SequenceEqual(held.Pixels) && !next.Pixels.SequenceEqual(held.Pixels),
            "Kinescope holds a 24 Hz exposure from a 48 fps video and reproduces it after seeking");
        var matches = false;
        var exported = await VideoExporter.ExportAsync(original, target, settings, cancellation: token, overwrite: true,
            frameObserver: (i, frame) => { if (i == 5) matches = frame.Pixels.SequenceEqual(afterJump.Pixels); });
        var audio = await MediaTools.CaptureAsync("ffprobe",
            ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=codec_type", "-of", "csv=p=0", target], token);
        check(matches && exported.Frames == source.Info.FrameCount && exported.Width == 320 && exported.Height == 240 &&
            audio.Trim() == "audio", "Kinescope MP4 matches the seeked preview and preserves the source timeline and audio");
    }

    private static async Task CheckHistoricalCombinations(string directory, string original, Action<bool, string> check, CancellationToken token)
    {
        await using var source = await VideoSource.OpenAsync(original, token);
        using var pipeline = new EffectPipeline();
        var basic = BasicLookCatalog.Groups.SelectMany(group => group.Looks).Single(look => look.Id == "antenna-ghost")
            .CreatePreset("crt-easymode");
        var preset = basic with { CrtEnabled = false, Display = HistoricalDisplay.British405, Film = true };
        check(LookPreset.Parse(JsonSerializer.Serialize(preset), [preset.Shader])
            is { Display: HistoricalDisplay.British405, Film: true, Analog.Reception: true },
            "A combined signal, screen and film preset preserves all stages");
        var settings = new RenderSettings(true, false, preset.Ntsc.ToJsonString(), "", 320, 3, 5, [],
            new(320, 240), DigitalCatalog.Profile, Analog: preset.Analog, Display: preset.Display);
        var requested = new List<int>();
        Task<PixelFrame> ReadSource(int index, CancellationToken cancellation)
        {
            requested.Add(index);
            return source.ReadFrameAsync(index, cancellation);
        }
        async Task<RenderResult> Preview(RenderSettings value) => await pipeline.RenderHistoricalVideoAsync(
            value, "combined-preview", source.Info, ReadSource, token);
        var ghost = await Preview(settings);
        check(requested.SequenceEqual(new[] { (int)settings.Frame }),
            "Seeking with fixed reception and NTSC settings does not request earlier source pictures");
        var clean = await Preview(settings with { Analog = null });
        check(!ghost.Frame.Pixels.SequenceEqual(clean.Frame.Pixels), "Reception changes a 405-line image and invalidates the held preview cache");
        var filmed = await Preview(settings with { Film = true });
        var target = Path.Combine(directory, "combined-405-kinescope.mp4");
        var matches = false;
        var exported = await VideoExporter.ExportAsync(original, target, settings with { Film = true }, cancellation: token, overwrite: true,
            frameObserver: (i, frame) => { if (i == settings.Frame) matches = frame.Pixels.SequenceEqual(filmed.Frame.Pixels); });
        check(matches && exported.Width == 320 && exported.Height == 240, "Reception, the historical display and film render identically after a seek and during export");

        var crt = settings with { Analog = null, Display = HistoricalDisplay.None, Film = true, Crt = true, Renderer = "legacy-v1",
            Shader = Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp") };
        var filmedCrt = await Preview(crt);
        var filmWithoutCrt = await Preview(crt with { Crt = false });
        check(!filmedCrt.Frame.Pixels.SequenceEqual(filmWithoutCrt.Frame.Pixels) &&
            filmedCrt.Parameters.Count > 0, "Filming a CRT preserves its rendered contribution and editable shader parameters");
        var filmSized = new PixelFrame(384, 288, Enumerable.Repeat((byte)127, 384 * 288 * 4).ToArray());
        pipeline.Render(filmSized, settings with { Display = HistoricalDisplay.None, Film = true, Ntsc = false, Analog = null });
        check(filmSized.Pixels.All(value => value == 127), "Kinescope rendering leaves the original comparison frame intact");
    }

    private static async Task CheckSideBySideExport(string directory, string original, Action<bool, string> check, CancellationToken token)
    {
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, [], new(192, 64),
            Crop: new("1:1", X: 1), Film: true);
        var target = Path.Combine(directory, "comparison.mp4");
        const int frameIndex = 5;
        PixelFrame? seen = null;
        var exported = await VideoExporter.ExportAsync(original, target, settings, cancellation: token, overwrite: true,
            frameObserver: (index, frame) => { if (index == frameIndex) seen = frame; }, sideBySide: true);
        await using var source = await VideoSource.OpenAsync(original, token);
        var input = await source.ReadFrameAsync(frameIndex, token);
        using var pipeline = new EffectPipeline();
        var rendered = await pipeline.RenderAsync(input, settings with { Frame = frameIndex }, "side-by-side-check",
            source.Info, source.ReadFrameAsync, token);
        var right = OutputComposer.Compose(rendered.Frame, new OutputSettings(96, 64).Geometry(rendered.Frame.Width, rendered.Frame.Height));
        check(seen is { Width: 192, Height: 64 } && PanelMatches(seen, input, 0) && PanelMatches(seen, right, 96),
            "Comparison export places the full source and the cropped, processed frame in the two panels");
        using var metadata = await Probe(target, token);
        var video = metadata.RootElement.GetProperty("streams").EnumerateArray()
            .Single(stream => stream.GetProperty("codec_type").GetString() == "video");
        check(exported is { Width: 192, Height: 64, Frames: 12 } && video.GetProperty("width").GetInt32() == 192 &&
            video.GetProperty("height").GetInt32() == 64 && await AudioAligned(target, token),
            "Comparison MP4 uses the selected total size and preserves delayed source audio");
        var gif = await VideoExporter.ExportAsync(original, Path.Combine(directory, "comparison.gif"), settings,
            cancellation: token, overwrite: true, format: VideoExportFormat.Gif, gifSettings: new(144, 8), sideBySide: true);
        check(gif is { Width: 144, Height: 48, Frames: 16 }, "GIF width and frame rate apply to the complete comparison");

        var fastSource = Path.Combine(directory, "kinescope-source.mp4");
        PixelFrame? before = null, after = null;
        var fastSettings = settings with { Output = new(640, 240), Crop = null };
        await VideoExporter.ExportAsync(fastSource, Path.Combine(directory, "comparison-film-cadence.mp4"), fastSettings,
            cancellation: token, overwrite: true, frameObserver: (index, frame) =>
            {
                if (index == 4) before = frame;
                if (index == 5) after = frame;
            }, sideBySide: true);
        await using var raw = await VideoSource.OpenAsync(fastSource, token);
        var rawAfter = await raw.ReadFrameAsync(5, token);
        check(after != null && before != null && PanelMatches(after, rawAfter, 0) && !PanelMatches(before, rawAfter, 0) &&
            Enumerable.Range(0, after.Height).All(y => SameRow(before, y, 320, after, y, 320, 320)),
            "Comparison keeps the original timeline when two consecutive output frames share a film exposure");

        static bool PanelMatches(PixelFrame canvas, PixelFrame panel, int left) =>
            Enumerable.Range(0, panel.Height).All(y => SameRow(canvas, y, left, panel, y, 0, panel.Width));

        static bool SameRow(PixelFrame a, int ay, int ax, PixelFrame b, int by, int bx, int width) =>
            a.Pixels.AsSpan((ay * a.Width + ax) * 4, width * 4).SequenceEqual(b.Pixels.AsSpan((by * b.Width + bx) * 4, width * 4));
    }

    private static async Task CheckPreviewAudio(string directory, string path, Action<bool, string> check, CancellationToken token)
    {
        await using var delayed = await VideoSource.OpenAsync(path, token);
        var info = delayed.Info;
        var pcm = await Decode(info, 0, info.FrameCount, token);
        var sampleAtSeek = (int)PreviewAudio.FrameSample(info, 3);
        var seek = await Decode(info, 3, info.FrameCount, token);
        check(info.HasAudio && pcm.Length == PreviewAudio.FrameSample(info, info.FrameCount) * PreviewAudio.BlockAlign &&
            Peak(pcm, 0, 0.25) <= 16 && Peak(pcm, 0.5, 0.2) > 1000,
            "Preview audio preserves the original delay relative to a nonzero video start");
        check(pcm.Skip(sampleAtSeek * PreviewAudio.BlockAlign).SequenceEqual(seek),
            "Preview audio after a seek matches the same samples on the continuous timeline");
        await using var silentVideo = await VideoSource.OpenAsync(Path.Combine(directory, "baird-portrait.mp4"), token);
        check(!silentVideo.Info.HasAudio, "Sources without an audio track do not offer preview audio");

        var vfrPath = Path.Combine(directory, "preview-audio-vfr.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-f", "lavfi", "-i",
            "testsrc2=size=96x64:rate=30:duration=1", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
            "-vf", "select='not(eq(mod(n,3),1))'", "-fps_mode", "vfr", "-c:v", "libx264", "-c:a", "aac",
            "-t", "1", vfrPath], token);
        await using var vfr = await VideoSource.OpenAsync(vfrPath, token);
        var vfrInfo = vfr.Info;
        var vfrSamples = await Decode(vfrInfo, 3, 9, token);
        check(vfrInfo.FramesPerSecond is > 15 and < 25 && vfrSamples.Length ==
            (PreviewAudio.FrameSample(vfrInfo, 9) - PreviewAudio.FrameSample(vfrInfo, 3)) * PreviewAudio.BlockAlign,
            "Preview audio seeks on the normalized timeline of a variable-frame-rate source");

        var player = new PreviewAudio(vfrInfo, 0, Task.CompletedTask);
        try
        {
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (!player.Failed && player.Elapsed < 0.04 && wait.Elapsed < TimeSpan.FromSeconds(2))
                await Task.Delay(10, token);
            if (!player.Failed)
            {
                await Task.Delay(180, token);
                var stalled = player.Elapsed;
                player.Present(5);
                wait.Restart();
                while (!player.Failed && player.Elapsed <= stalled + 0.04 && wait.Elapsed < TimeSpan.FromSeconds(2))
                    await Task.Delay(10, token);
                check(!player.Failed && stalled is > 0.04 and <= 0.14 && player.Elapsed > stalled + 0.04,
                    "The Windows audio clock waits for displayed frames and resumes on presentation");
            }
        }
        finally { await player.StopAsync(); }

        static async Task<byte[]> Decode(VideoInfo info, int start, int end, CancellationToken cancellation)
        {
            using var process = PreviewAudio.StartDecoder(info, PreviewAudio.FrameSample(info, start), PreviewAudio.FrameSample(info, end));
            using var registration = cancellation.Register(() => MediaTools.Stop(process));
            using var bytes = new MemoryStream();
            var error = process.StandardError.ReadToEndAsync();
            await process.StandardOutput.BaseStream.CopyToAsync(bytes, cancellation);
            await process.WaitForExitAsync(cancellation);
            if (process.ExitCode != 0) throw new InvalidDataException(await error);
            return bytes.ToArray();
        }

        static int Peak(byte[] pcm, double start, double length)
        {
            var peak = 0;
            var end = (int)((start + length) * PreviewAudio.SampleRate) * PreviewAudio.BlockAlign;
            for (var offset = (int)(start * PreviewAudio.SampleRate) * PreviewAudio.BlockAlign; offset < end; offset += 2)
                peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, offset)));
            return peak;
        }
    }

    private static async Task CheckHistoricalLooks(string directory, string sourcePath, Action<bool, string> check, CancellationToken token)
    {
        var looks = MainWindow.CheckBasicLookSelection(check);
        await using var source = await VideoSource.OpenAsync(sourcePath, token);
        using var preview = new EffectPipeline();
        const int index = 5;
        async Task<PixelFrame> Render(string id) => (await preview.RenderAsync(await source.ReadFrameAsync(index, token),
            looks[id] with { Frame = index }, id, source.Info, source.ReadFrameAsync, token)).Frame;
        var clean = await Render("vhs-crt");
        var worn = await Render("used-vhs");
        var ghost = await Render("antenna-ghost");
        check(!clean.Pixels.SequenceEqual(worn.Pixels) && !clean.Pixels.SequenceEqual(ghost.Pixels) &&
            !worn.Pixels.SequenceEqual(ghost.Pixels), "Tape wear and antenna ghosting produce distinct images");
        var expectedFrames = new Dictionary<string, PixelFrame> { ["used-vhs"] = worn, ["antenna-ghost"] = ghost };
        foreach (var id in new[] { "ntscrt-clean-vhs", "ntscrt-clean-crt", "ntscrt-aperture-mask" })
            expectedFrames[id] = await Render(id);
        foreach (var (id, expected) in expectedFrames)
        {
            var target = Path.Combine(directory, $"basic-{id}.mp4");
            var matches = false;
            var exported = await VideoExporter.ExportAsync(sourcePath, target, looks[id], cancellation: token, overwrite: true,
                frameObserver: (i, frame) => { if (i == index) matches = frame.Pixels.SequenceEqual(expected.Pixels); });
            check(matches && exported.Frames == source.Info.FrameCount && exported.Width == 176 && exported.Height == 144,
                $"Basic {id} reproduces the preview frame and output geometry in the full export");
        }
    }

    private static async Task CheckStillWithoutKeys(string directory, Action<bool, string> check, CancellationToken token)
    {
        var image = PixelFrame.TestPattern();
        var ntsc = NtscProcessor.Defaults();
        ntsc["composite_noise"] = true;
        ntsc["composite_noise_intensity"] = 0.5;
        var settings = new RenderSettings(true, false, ntsc.ToJsonString(), "", 96, 2, 0, [],
            new(96, 64), DigitalCatalog.Profile);
        var mp4 = Path.Combine(directory, "still-without-keys.mp4");
        PixelFrame? first = null, last = null;
        var result = await VideoExporter.ExportStillAsync(image, null, mp4, 0.5, settings,
            cancellation: token, overwrite: true, fps: 24, frameObserver: (index, frame) =>
            {
                if (index == 0) first = frame;
                last = frame;
            });
        await using var clip = await VideoSource.OpenAsync(mp4, token);
        check(result.Frames == 12 && clip.Info is { FramesPerSecond: 24, FrameCount: 12 } &&
            first != null && last != null && !first.Pixels.AsSpan().SequenceEqual(last.Pixels),
            "MP4 aus einem Standbild ohne Keyframes übernimmt Bildrate und animiert NTSC-Rauschen");
        var gif = await VideoExporter.ExportStillAsync(image, null, Path.Combine(directory, "still-without-keys.gif"), 0.5, settings,
            cancellation: token, overwrite: true, fps: 12, format: VideoExportFormat.Gif);
        check(gif.Frames == 6, "GIF aus einem Standbild ohne Keyframes übernimmt 12 Bilder pro Sekunde");
    }

    private static async Task<JsonDocument> Probe(string path, CancellationToken cancellation) => JsonDocument.Parse(
        await MediaTools.CaptureAsync("ffprobe", ["-v", "error", "-count_frames", "-show_streams", "-of", "json", path], cancellation));

    // Check when the tone becomes audible, including padding and codec delay.
    private static async Task<bool> AudioAligned(string path, CancellationToken token)
    {
        using var decode = MediaTools.Start("ffmpeg", ["-v", "error", "-nostdin", "-i", path,
            "-map", "0:a:0", "-ac", "1", "-ar", "48000", "-f", "f32le", "pipe:1"]);
        using var registration = token.Register(() => MediaTools.Stop(decode));
        var errors = decode.StandardError.ReadToEndAsync();
        using var memory = new MemoryStream();
        await decode.StandardOutput.BaseStream.CopyToAsync(memory, token);
        await decode.WaitForExitAsync(token);
        if (decode.ExitCode != 0) throw new InvalidOperationException(await errors);
        var bytes = memory.ToArray();
        double Energy(double from, double to)
        {
            double sum = 0;
            var start = (int)(from * 48000);
            var end = Math.Min((int)(to * 48000), bytes.Length / 4);
            for (var sample = start; sample < end; sample++)
            {
                var value = BitConverter.ToSingle(bytes, sample * 4);
                sum += value * value;
            }
            return sum / Math.Max(1, end - start);
        }
        return Math.Abs(bytes.Length / (48000.0 * 4) - 2) < 0.05 &&
            Energy(0.03, 0.3) < 0.000001 && Energy(0.55, 0.75) > 0.001 && Energy(1.4, 1.8) < 0.000001;
    }
}
