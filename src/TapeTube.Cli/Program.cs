// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Text.Json;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal static class Program
{
    private const string Usage = """
        Tape & Tube command line

          TapeTube.Cli describe [processing|ntsc|digital|vcr|reception|crt|screen-loop|all] [--shader NAME]
          TapeTube.Cli mcp
          TapeTube.Cli render --input FILE --output FILE [--preset FILE] [--effects JSON]
              [--width PX --height PX] [--frame N | --time S[,S…]] [--seconds N --fps N] [--snap] [--overwrite] [--dry-run]
              [--codec h264|hevc] [--quality standard|high|very-high|maximum] [--loops 1..50]
              [--gif-width 64..1920] [--gif-fps 1..50]
              [--zoom 1..12] [--zoom-at X,Y]

        Output format follows the extension: PNG, MP4 or GIF.
        --snap adjusts the output size to the scanline grid.
        --codec selects the MP4 video codec. Default: h264. HEVC requires FFmpeg with libx265.
        --quality selects the MP4 video quality. Default: high.
        --loops sets total plays of a video input in the MP4. Default: 1.
        --frame selects a zero-based frame for PNG.
        --time selects one or more PNG times in seconds, e.g. 0,1.5,3.
        --zoom magnifies a rendered PNG around --zoom-at X,Y (0..1, default 0.5,0.5).
        --seconds and --fps set the timing of animations made from still images.
        --effects applies a JSON object of stage overrides. Example: {"ntsc":{"enabled":true}}.
        --dry-run reports the resolved render settings without writing output.
        JSON results go to standard output. Errors go to standard error.
        """;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };
    private static readonly Dictionary<string, Mp4Quality> Mp4Qualities = new(StringComparer.Ordinal)
    {
        ["standard"] = Mp4Quality.Standard,
        ["high"] = Mp4Quality.High,
        ["very-high"] = Mp4Quality.VeryHigh,
        ["maximum"] = Mp4Quality.Maximum
    };

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args is ["--help"] or ["help"] or ["render", "--help"] or ["describe", "--help"])
        {
            Console.WriteLine(Usage);
            return 0;
        }
        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancellation.Cancel(); };
        Console.CancelKeyPress += cancel;
        try
        {
            if (args[0] == "describe")
            {
                (string? stage, string? shader) = args switch
                {
                    ["describe"] => (null, null),
                    ["describe", var name] => (name, null),
                    ["describe", "--shader", var selected] => (null, selected),
                    ["describe", var name, "--shader", var selected] => (name, selected),
                    _ => throw new ArgumentException("Expected describe [stage] [--shader NAME]. See --help.")
                };
                if (stage != null || shader != null) Components.ConfigureNativeLibraries();
                Console.WriteLine(JsonSerializer.Serialize(Describe(stage, shader), CoreWorkerClient.Json));
                return 0;
            }
            if (args is ["mcp"])
            {
                Components.ConfigureNativeLibraries();
                McpServer.RunAsync(cancellation.Token).GetAwaiter().GetResult();
                return 0;
            }
            if (args[0] != "render") throw new ArgumentException("Expected render or describe. See --help.");
            var options = Parse(args.AsSpan(1));
            Components.ConfigureNativeLibraries();
            var result = RenderAsync(options, cancellation.Token).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(result, CoreWorkerClient.Json));
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Export cancelled."); return 130; }
        catch (ArgumentException ex) { Console.Error.WriteLine(ex.Message); return 2; }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        finally { Console.CancelKeyPress -= cancel; }
    }

    internal static object Describe(string? stage = null, string? shader = null)
    {
        var shaders = Components.AvailableShaderPresets();
        return new
        {
            renderer = DigitalCatalog.Profile,
            ntscrtRevision = DigitalCatalog.Revision,
            shaders = shaders.Keys,
            digitalFormats = DigitalCatalog.Looks.Select(look => look.Id),
            outputs = new[] { "png", "mp4", "gif" },
            mp4Codecs = new[] { "h264", "hevc" },
            mp4Qualities = Mp4Qualities.Keys,
            maxMp4Loops = VideoExporter.MaxLoops,
            effectStages = EffectCatalog.Stages,
            effects = EffectCatalog.Describe(stage, shader, shaders)
        };
    }

    private static Dictionary<string, string> Parse(ReadOnlySpan<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (options.ContainsKey(name)) throw new ArgumentException("Repeated option: " + name);
            if (name is "--overwrite" or "--snap" or "--dry-run")
            {
                options.Add(name, "");
                continue;
            }
            if (name is not ("--input" or "--output" or "--preset" or "--effects" or "--width" or "--height" or "--frame" or "--time" or "--zoom" or "--zoom-at" or "--seconds" or "--fps" or "--codec" or "--quality" or "--loops" or "--gif-width" or "--gif-fps"))
                throw new ArgumentException("Unknown option: " + name);
            if (++i == args.Length || args[i].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Missing value for " + name);
            options.Add(name, args[i]);
        }
        if (!options.ContainsKey("--input") || !options.ContainsKey("--output"))
            throw new ArgumentException("Both --input and --output are required.");
        return options;
    }

    private sealed record RenderLook(RenderSettings Settings, double Seconds = 5, int Fps = 30, bool HasTimeline = false);

    private static RenderLook ReadLook(string? path)
    {
        var settings = new RenderSettings(false, false, "{}", "", 320, 3, 0, []);
        if (path == null) return new(settings);
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Preset exceeds 1 MB.");
        var text = File.ReadAllText(path);
        if (NtscrtImportedLook.Parse(text) is { } imported)
            return new(settings with { Crt = imported.Crt, RetroWidth = imported.RetroWidth,
                Renderer = DigitalCatalog.Profile, ImportedLook = imported, Crop = imported.Crop },
                imported.Seconds, imported.Fps, imported.KeyframeCount > 0);

        using var json = JsonDocument.Parse(text);
        if (json.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == ScreenLoopPreset.PresetKind)
        {
            var loop = ScreenLoopPreset.Parse(text);
            return new(settings with { Renderer = DigitalCatalog.Profile, ScreenLoop = loop.Values }, loop.Seconds ?? 5,
                HasTimeline: true);
        }

        var shaders = Components.AvailableShaderPresets();
        var preset = LookPreset.Parse(text, shaders.Keys);
        var ntsc = NtscProcessor.Defaults();
        foreach (var (key, value) in preset.Ntsc) ntsc[key] = value?.DeepClone();
        var track = preset.Timeline;
        var animated = track?.Enabled == true && preset.Display == HistoricalDisplay.None && !preset.Film;
        return new(new(preset.NtscEnabled, preset.CrtEnabled && preset.Display == HistoricalDisplay.None, ntsc.ToJsonString(), shaders[preset.Shader],
            preset.Width, preset.Scale, 0, preset.Parameters, preset.Output,
            animated || preset.DownscaleMethod != null || preset.DownscaleEnabled.HasValue
                ? DigitalCatalog.Profile : preset.Renderer, preset.Digital, preset.Analog,
            EffectTimeline: animated && track is { Keys.Length: > 0 } ? track.WorkerTimeline(preset.Shader) : null,
            Crop: preset.Crop, DownscaleMethod: preset.DownscaleMethod, DownscaleEnabled: preset.DownscaleEnabled,
            Display: preset.Display, Film: preset.Film),
            animated ? track!.Seconds : 5, animated ? track!.Fps : 30, animated);
    }

    internal static async Task<object> RenderAsync(Dictionary<string, string> options, CancellationToken token)
    {
        var source = Path.GetFullPath(options["--input"]);
        var overwrite = options.ContainsKey("--overwrite");
        var dryRun = options.ContainsKey("--dry-run");
        var times = options.TryGetValue("--time", out var timeList) ? PngSequence.Parse(timeList) : null;
        var output = dryRun || times != null ? Path.GetFullPath(options["--output"])
            : VideoExporter.CheckTarget(source, options["--output"], overwrite);
        var isImage = ImageExtensions.Contains(Path.GetExtension(source));
        var extension = Path.GetExtension(output).ToLowerInvariant();
        if (extension is not (".png" or ".mp4" or ".gif")) throw new ArgumentException("Choose .png, .mp4 or .gif for --output.");
        var codec = options.GetValueOrDefault("--codec");
        if (codec != null && (extension != ".mp4" || codec is not ("h264" or "hevc")))
            throw new ArgumentException("--codec must be h264 or hevc and is only available for MP4.");
        var quality = Mp4Quality.High;
        if (options.TryGetValue("--quality", out var qualityName) &&
            (extension != ".mp4" || !Mp4Qualities.TryGetValue(qualityName, out quality)))
            throw new ArgumentException("--quality must be standard, high, very-high or maximum and is only available for MP4.");
        if (options.ContainsKey("--loops") && (isImage || extension != ".mp4"))
            throw new ArgumentException("--loops requires video input and MP4 output.");
        var loops = Number("--loops", 1, 1, VideoExporter.MaxLoops);
        if (extension != ".png" && options.ContainsKey("--frame")) throw new ArgumentException("--frame is only available for PNG.");
        if (times != null && (extension != ".png" || options.ContainsKey("--frame")))
            throw new ArgumentException("--time requires PNG output and cannot be combined with --frame.");
        if (!isImage && (options.ContainsKey("--fps") || options.ContainsKey("--seconds")))
            throw new ArgumentException("--seconds and --fps apply to still image inputs.");
        var hasGifWidth = options.ContainsKey("--gif-width");
        var hasGifFps = options.ContainsKey("--gif-fps");
        if (extension != ".gif" && (hasGifWidth || hasGifFps))
            throw new ArgumentException("--gif-width and --gif-fps are only available for GIF.");
        if (hasGifWidth && (options.ContainsKey("--width") || options.ContainsKey("--height")))
            throw new ArgumentException("Use --gif-width or --width and --height together.");
        var gifSettings = new GifExportSettings(
            hasGifWidth ? Number("--gif-width", 0, GifExportSettings.MinWidth, GifExportSettings.MaxWidth) : null,
            hasGifFps ? Number("--gif-fps", 0, 1, GifExportSettings.MaxFps) : null);

        var look = ReadLook(options.GetValueOrDefault("--preset"));
        var settings = EffectOverrides.Apply(options.GetValueOrDefault("--effects"), look.Settings)
            with { SnapScanlines = options.ContainsKey("--snap") };
        if (options.ContainsKey("--width") || options.ContainsKey("--height"))
        {
            if (!options.ContainsKey("--width") || !options.ContainsKey("--height"))
                throw new ArgumentException("Supply --width and --height together.");
            var size = new OutputSettings(Number("--width", 0, 1, 8192), Number("--height", 0, 1, 8192), settings.Output?.Fit ?? OutputFit.Fit);
            size.Validate();
            settings = settings with { Output = size };
        }
        var seconds = look.Seconds;
        if (options.TryGetValue("--seconds", out var duration) &&
            (!double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) ||
             !double.IsFinite(seconds) || seconds is < 0.5 or > NtscrtImportedLook.MaxStillSeconds))
            throw new ArgumentException("--seconds must be between 0.5 and 120.");
        if (settings.ScreenLoop != null && seconds > 60) throw new ArgumentException("Screen Loop supports at most 60 seconds.");
        var fps = Number("--fps", look.Fps, 1, 60);
        var format = extension == ".gif" ? VideoExportFormat.Gif :
            codec == "hevc" ? VideoExportFormat.Hevc : VideoExportFormat.Mp4;
        var png = extension == ".png";
        var closeup = PngCloseup.Parse(options.GetValueOrDefault("--zoom"), options.GetValueOrDefault("--zoom-at"), png);
        var stillHasTimeline = !png || times != null || look.HasTimeline || settings.ScreenLoop != null ||
            options.ContainsKey("--frame") || options.ContainsKey("--seconds") || options.ContainsKey("--fps");

        token.ThrowIfCancellationRequested();
        if (dryRun)
        {
            VideoInfo info;
            if (isImage)
            {
                var (width, height) = PixelFrame.ReadDimensions(source);
                info = stillHasTimeline ? ScreenLoopCatalog.StillTimeline(width, height, seconds, fps)
                    : new VideoInfo("", width, height, fps, FormattableString.Invariant($"{fps}/1"), 1, 0, IsStill: true);
            }
            else
            {
                await using var probe = await VideoSource.OpenAsync(source, token).ConfigureAwait(false);
                info = probe.Info;
            }
            var index = png && times == null ? Number("--frame", 0, 0, info.FrameCount - 1) : (int?)null;
            var positions = times == null ? null : PngSequence.Positions(output, info, times);
            return RenderPlan.Describe(source, output, info, settings, index, format, quality, loops, gifSettings, overwrite, positions, closeup);
        }
        if (isImage)
        {
            var image = PixelFrame.Read(source);
            if (!png)
                return await VideoExporter.ExportStillAsync(image, source, output, seconds, settings,
                    cancellation: token, overwrite: overwrite, fps: fps, format: format, quality: quality,
                    gifSettings: gifSettings).ConfigureAwait(false);
            var timeline = stillHasTimeline ? ScreenLoopCatalog.StillTimeline(image, seconds, fps) : null;
            if (times != null)
                return await PngSequence.ExportAsync(source, PngSequence.Positions(output, timeline!, times), timeline!, settings,
                    (_, _) => Task.FromResult(image), overwrite, closeup, token).ConfigureAwait(false);
            var index = Number("--frame", 0, 0, (timeline?.FrameCount ?? 1) - 1);
            return await SavePngAsync(image, output, settings, index, timeline, null, overwrite, closeup, token).ConfigureAwait(false);
        }
        if (!png)
            return await VideoExporter.ExportAsync(source, output, settings, cancellation: token,
                overwrite: overwrite, format: format, quality: quality, loops: loops, gifSettings: gifSettings).ConfigureAwait(false);
        await using var video = await VideoSource.OpenAsync(source, token).ConfigureAwait(false);
        if (times != null)
            return await PngSequence.ExportAsync(source, PngSequence.Positions(output, video.Info, times), video.Info, settings,
                video.ReadFrameAsync, overwrite, closeup, token).ConfigureAwait(false);
        var frameIndex = Number("--frame", 0, 0, video.Info.FrameCount - 1);
        var frame = await video.ReadFrameAsync(frameIndex, token).ConfigureAwait(false);
        return await SavePngAsync(frame, output, settings, frameIndex, video.Info, video.ReadFrameAsync, overwrite, closeup, token).ConfigureAwait(false);

        int Number(string name, int fallback, int min, int max)
        {
            if (!options.TryGetValue(name, out var value)) return fallback;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max)
                throw new ArgumentException($"{name} must be an integer from {min} to {max}.");
            return number;
        }
    }

    private static async Task<ExportResult> SavePngAsync(PixelFrame source, string output, RenderSettings settings, int index,
        VideoInfo? info, Func<int, CancellationToken, Task<PixelFrame>>? history, bool overwrite, PngCloseup? closeup,
        CancellationToken token)
    {
        using var pipeline = new EffectPipeline();
        var rendered = await pipeline.RenderAsync(source, settings with { Frame = (uint)index }, "command-line",
            info, history, token).ConfigureAwait(false);
        var image = closeup?.Apply(rendered.Frame, token) ?? rendered.Frame;
        var temp = Path.Combine(Path.GetDirectoryName(output)!, $".tapetube-{Guid.NewGuid():N}.png");
        try
        {
            token.ThrowIfCancellationRequested();
            image.Save(temp);
            token.ThrowIfCancellationRequested();
            File.Move(temp, output, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return new(output, 1, image.Width, image.Height, 0);
    }
}
