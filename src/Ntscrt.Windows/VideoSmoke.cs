// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class VideoSmoke
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(Paths.Root, "artifacts", "video-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "video-smoke-error.txt"), "");
        var path = Path.Combine(directory, "Video test ä.mp4");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = timeout.Token;
        await MediaTools.CaptureAsync("ffmpeg", ["-hide_banner", "-loglevel", "error", "-nostdin", "-y",
            "-f", "lavfi", "-i", "testsrc2=size=160x120:rate=30000/1001:duration=2", "-f", "lavfi", "-i", "sine=frequency=440:duration=2",
            "-c:v", "mpeg4", "-q:v", "2", "-g", "10", "-c:a", "aac", path], token);
        var checks = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); checks.Add(name); }

        await using var video = await VideoSource.OpenAsync(path, token);
        Check(video.Info.Width == 160 && video.Info.Height == 120, "Videogröße");
        Check(Math.Abs(video.Info.FramesPerSecond - 30000.0 / 1001) < 0.00001, "Gebrochene Bildrate");
        Check(video.Info.FrameCount == 60, "Bildanzahl");
        var first = await video.ReadFrameAsync(0, token);
        var one = await video.ReadFrameAsync(1, token);
        Check(!first.Pixels.SequenceEqual(one.Pixels), "Aufeinanderfolgende Videobilder unterscheiden sich");
        PixelFrame? third = null, twentieth = null;
        for (int i = 2; i <= 20; i++)
        {
            var frame = await video.ReadFrameAsync(i, token);
            if (i == 3) third = frame;
            if (i == 20) twentieth = frame;
        }
        var seekThird = await video.ReadFrameAsync(3, token);
        Check(third!.Pixels.SequenceEqual(seekThird.Pixels), "Rückwärtssprung entspricht fortlaufender Dekodierung");
        await using var direct = await VideoSource.OpenAsync(path, token);
        var seekTwentieth = await direct.ReadFrameAsync(20, token);
        Check(twentieth!.Pixels.SequenceEqual(seekTwentieth.Pixels), "Direktsprung entspricht fortlaufender Dekodierung");
        var last = await video.ReadFrameAsync(video.Info.FrameCount - 1, token);
        Check(last.Pixels.Length == 160 * 120 * 4, "Letztes Bild lesbar");
        var invalid = false;
        try { await video.ReadFrameAsync(video.Info.FrameCount, token); } catch (ArgumentOutOfRangeException) { invalid = true; }
        Check(invalid, "Position hinter Videoende abgelehnt");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel(); var rejected = false;
            try { await video.ReadFrameAsync(10, cancelled.Token); } catch (OperationCanceledException) { rejected = true; }
            Check(rejected, "Abbruch respektiert");
        }
        Check((await video.ReadFrameAsync(1, token)).Pixels.SequenceEqual(one.Pixels), "Dekoder nach Abbruch weiterhin nutzbar");

        using var pipeline = new EffectPipeline();
        var settings = new RenderSettings(true, true, NtscProcessor.Defaults().ToJsonString(),
            Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp"), 320, 3, 3, []);
        var effected = pipeline.Render(third, settings).Frame;
        Check(effected.Width == 480 && effected.Height == 360, "Videobild durch NTSC und CRT verarbeitet");
        Check(effected.Pixels.SequenceEqual(pipeline.Render(third, settings).Frame.Pixels), "Pausiertes Effektbild reproduzierbar");
        Check(!effected.Pixels.SequenceEqual(pipeline.Render(third, settings with { Frame = 4 }).Frame.Pixels), "Effekte verwenden die Videobildnummer");
        var png = Path.Combine(Paths.Root, "artifacts", "video-frame.png");
        effected.Save(png);
        Check(PixelFrame.Read(png).Pixels.SequenceEqual(effected.Pixels), "PNG-Export des aktuellen Videobildes");

        var portraitPath = Path.Combine(directory, "portrait.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-display_rotation", "90", "-i", path, "-c", "copy", portraitPath], token);
        await using var portrait = await VideoSource.OpenAsync(portraitPath, token);
        var rotated = await portrait.ReadFrameAsync(0, token);
        Check(rotated.Width == 120 && rotated.Height == 160, "Rotationsmetadaten bei Hochkantvideo");

        var vfrPath = Path.Combine(directory, "variable-rate.mp4");
        await MediaTools.CaptureAsync("ffmpeg", ["-v", "error", "-nostdin", "-y", "-i", path, "-an", "-vf", "select=not(mod(n\\,2))+not(mod(n\\,5))",
            "-fps_mode", "vfr", "-c:v", "mpeg4", "-q:v", "2", vfrPath], token);
        await using var vfr = await VideoSource.OpenAsync(vfrPath, token);
        var vfrIndex = Math.Min(10, vfr.Info.FrameCount - 1);
        PixelFrame? reference = null;
        for (int i = 0; i <= vfrIndex; i++) reference = await vfr.ReadFrameAsync(i, token);
        await vfr.ReadFrameAsync(0, token);
        var vfrSeek = await vfr.ReadFrameAsync(vfrIndex, token);
        Check(reference!.Pixels.SequenceEqual(vfrSeek.Pixels), "Variable Bildrate: Springen entspricht Vorschau-Zeitraster");
        await vfr.ReadFrameAsync(vfr.Info.FrameCount - 1, token);
        checks.Add("Variable Bildrate: Videoende erreichbar");

        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "video-smoke.json"), JsonSerializer.Serialize(new
        {
            passed = true, count = checks.Count, checks, source = path,
            video.Info.FramesPerSecond, video.Info.FrameCount, png,
            ffmpeg = MediaTools.Find("ffmpeg")
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
