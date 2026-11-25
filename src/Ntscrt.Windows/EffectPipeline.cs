// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

internal sealed record RenderSettings(bool Ntsc, bool Crt, string NtscJson, string Shader,
    int RetroWidth, int Scale, uint Frame, Dictionary<string, float> ShaderValues, OutputSettings? Output = null,
    string Renderer = "legacy-v1", Dictionary<string,double>? Digital = null, AnalogSettings? Analog = null,
    Dictionary<string, double>? ScreenLoop = null, NtscrtImportedLook? ImportedLook = null,
    System.Text.Json.Nodes.JsonObject? EffectTimeline = null, SourceCrop? Crop = null,
    bool SnapScanlines = false, int ScanlineSizeLimit = OutputSettings.MaxSide, string? DownscaleMethod = null,
    bool? DownscaleEnabled = null);
internal sealed record RenderResult(PixelFrame Frame, IReadOnlyList<ShaderParameter> Parameters, OutputGeometry? Layout = null,
    int ScanlineRows = 0);
internal sealed record EffectOutputPlan(int RenderWidth, int RenderHeight, OutputGeometry? Layout, int ScanlineRows)
{
    public int Width => Layout?.Width ?? RenderWidth;
    public int Height => Layout?.Height ?? RenderHeight;
}

internal sealed class EffectPipeline : IDisposable
{
    private readonly object _gate = new();
    private CrtProcessor? _crt;
    private readonly CoreWorkerClient _worker = new();
    public int? WorkerProcessId => _worker.ProcessId;
    public string? WorkerAdapter => _worker.Adapter?.ToJsonString();
    public (string? Name, string? Backend) GraphicsAdapter
    {
        get
        {
            var adapter = _worker.Adapter;
            return (adapter?["name"]?.ToString(), adapter?["backend"]?.ToString());
        }
    }
    public string? D3D11Adapter => _crt?.AdapterName;
    public static string WorkerPath => Components.ConfiguredPath("coreWorker", "TAPETUBE_CORE_WORKER")
        ?? System.IO.Path.Combine(AppContext.BaseDirectory, "core", "TapeTube.Core.exe");
    public async Task<RenderResult> RenderAsync(PixelFrame source, RenderSettings settings, string sourceId,
        VideoInfo? video = null, Func<int,CancellationToken,Task<PixelFrame>>? history = null, CancellationToken cancellation = default, bool singleFrame = false)
    {
        if(settings.Renderer=="legacy-v1")
        {
            if(settings.Digital!=null || settings.Analog?.Active == true || settings.ScreenLoop != null || settings.ImportedLook != null || settings.EffectTimeline != null)
                throw new InvalidOperationException("Digital video, VCR, TV reception and Screen Loop require the shared renderer.");
            cancellation.ThrowIfCancellationRequested();return Render(source,settings);
        }
        if(settings.Renderer!=DigitalCatalog.Profile)throw new InvalidOperationException("Unsupported renderer profile.");
        if (settings.Crop is { } crop)
        {
            var rect = crop.Rectangle(source.Width, source.Height);
            sourceId = FormattableString.Invariant($"{sourceId}/crop/{rect.X}/{rect.Y}/{rect.Width}/{rect.Height}");
            source = crop.Apply(source);
        }
        if(settings.Digital!=null && settings.ScreenLoop == null)DigitalCatalog.Validate(settings.Digital);
        if (settings.Analog != null) AnalogCatalog.Current.Validate(settings.Analog);
        if (settings.ScreenLoop != null) ScreenLoopCatalog.Current.Validate(settings.ScreenLoop);
        if(!System.IO.File.Exists(WorkerPath))throw new System.IO.FileNotFoundException("Shared core worker is missing. Please rebuild the application.",WorkerPath);
        var plan = PlanCroppedOutput(source.Width, source.Height, settings);
        var layout = plan.Layout;
        var rate=(video?.Rate??"30/1").Split('/');
        var numerator=int.Parse(rate[0],System.Globalization.CultureInfo.InvariantCulture);
        var denominator=rate.Length==2?int.Parse(rate[1],System.Globalization.CultureInfo.InvariantCulture):1;
        var configuration=new System.Text.Json.Nodes.JsonObject{
            ["sourceId"]=sourceId,["width"]=source.Width,["height"]=source.Height,["frames"]=video?.FrameCount??Math.Max(1,checked((int)settings.Frame+1)),
            ["fpsNum"]=numerator,["fpsDen"]=denominator,["still"]=video==null || video.IsStill,
            ["ntscJson"]=settings.Ntsc?settings.NtscJson:null,["shader"]=settings.Crt && settings.ImportedLook == null ?settings.Shader:null,["shaderRoot"]=Paths.ShaderRoot,
            ["shaderValues"]=System.Text.Json.JsonSerializer.SerializeToNode(settings.ShaderValues),["retroWidth"]=settings.RetroWidth,
            ["outputWidth"]=plan.RenderWidth,["outputHeight"]=plan.RenderHeight,
            ["digital"]=System.Text.Json.JsonSerializer.SerializeToNode(settings.ScreenLoop == null ? settings.Digital : null)
        };
        if (settings.DownscaleMethod != null && settings.ImportedLook == null)
            configuration["downscaleMethod"] = settings.DownscaleMethod;
        if (settings.DownscaleEnabled.HasValue && settings.ImportedLook == null)
            configuration["downscaleEnabled"] = settings.DownscaleEnabled.Value;
        if (settings.ScreenLoop != null)
            configuration["screenLoop"] = System.Text.Json.JsonSerializer.SerializeToNode(settings.ScreenLoop);
        if (settings.ImportedLook != null)
            configuration["importedLook"] = settings.ImportedLook.Json;
        if (settings.EffectTimeline != null)
            configuration["effectTimeline"] = settings.EffectTimeline.DeepClone();
        if (settings.Analog?.Active == true)
            configuration["analog"] = System.Text.Json.JsonSerializer.SerializeToNode(
                new { settings.Analog.Vcr, settings.Analog.Reception, settings.Analog.Values }, CoreWorkerClient.Json);
        Func<int,CancellationToken,Task<CorePixels>>? provider = history == null ? null : async (i, t) =>
        {
            var prior = await history(i, t).ConfigureAwait(false);
            return ToCore(settings.Crop?.Apply(prior) ?? prior);
        };
        var result=await _worker.RenderAsync(WorkerPath,configuration,checked((int)settings.Frame),ToCore(source),provider,cancellation,singleFrame).ConfigureAwait(false);
        var pixels=SwapChannels(result.Pixels.Rgba);
        var frame=new PixelFrame(result.Pixels.Width,result.Pixels.Height,pixels);
        var parameters=result.Parameters.Select(p=>new ShaderParameter(p!["name"]!.ToString(),p["label"]!.ToString(),p["initial"]!.GetValue<float>(),p["minimum"]!.GetValue<float>(),p["maximum"]!.GetValue<float>(),p["step"]!.GetValue<float>())).ToArray();
        return new(layout==null?frame:OutputComposer.Compose(frame,layout),parameters,layout,plan.ScanlineRows);
    }
    internal static CorePixels ToCore(PixelFrame frame)=>new(frame.Width,frame.Height,SwapChannels(frame.Pixels));
    internal static byte[] SwapChannels(byte[] source)
    {
        var pixels=(byte[])source.Clone();
        for(int i=0;i<pixels.Length;i+=4)(pixels[i],pixels[i+2])=(pixels[i+2],pixels[i]);
        return pixels;
    }
    public RenderResult Render(PixelFrame source, RenderSettings settings)
    {
        if(settings.Renderer!="legacy-v1")return RenderAsync(source,settings,Guid.NewGuid().ToString("N")).GetAwaiter().GetResult();
        lock (_gate)
        {
            if (settings.Crop is { } crop) source = crop.Apply(source);
            var plan = PlanCroppedOutput(source.Width, source.Height, settings);
            var layout = plan.Layout;
            var frame = new PixelFrame(source.Width, source.Height, (byte[])source.Pixels.Clone());
            if (settings.Ntsc) NtscProcessor.Process(frame, settings.NtscJson, settings.Frame);
            if (settings.Crt) frame = frame.Resize(Math.Clamp(settings.RetroWidth, 64, 1280));
            if (!settings.Crt) return new(layout == null ? frame : OutputComposer.Compose(frame, layout), [], layout, plan.ScanlineRows);
            _crt ??= new CrtProcessor();
            _crt.Load(settings.Shader);
            var rendered = _crt.Render(frame, plan.RenderWidth, plan.RenderHeight, settings.Frame, settings.ShaderValues);
            return new(layout == null ? rendered : OutputComposer.Compose(rendered, layout), _crt.Parameters, layout, plan.ScanlineRows);
        }
    }

    internal static EffectOutputPlan PlanOutput(int width, int height, RenderSettings settings)
    {
        var crop = settings.Crop?.Rectangle(width, height);
        return PlanCroppedOutput(crop?.Width ?? width, crop?.Height ?? height, settings);
    }

    private static EffectOutputPlan PlanCroppedOutput(int width, int height, RenderSettings settings)
    {
        var automatic = (Width: width, Height: height);
        var lines = height;
        if (settings.Renderer == "legacy-v1")
        {
            if (settings.Crt)
            {
                var target = Math.Min(width, Math.Clamp(settings.RetroWidth, 64, 1280));
                // WPF's TransformedBitmap rounds the scaled height to the nearest integer, with half pixels rounded up.
                lines = Math.Max(1, (int)((target / (double)width) * height + 0.5));
                automatic = (checked(target * settings.Scale), checked(lines * settings.Scale));
            }
        }
        else
        {
            if (settings.Crt && (settings.ImportedLook?.Downscale ?? settings.DownscaleEnabled) != false)
            {
                var retroHeight = Math.Max(16, 2 * (int)Math.Round(settings.RetroWidth * height / (double)width / 2));
                automatic = (settings.RetroWidth * settings.Scale, retroHeight * settings.Scale);
            }
            if (UsesDownscale(settings)) lines = SharedScanlines(width, height, settings);
        }
        var layout = settings.SnapScanlines
            ? (settings.Output ?? new OutputSettings(automatic.Width, automatic.Height))
                .ScanlineGeometry(width, height, lines, settings.ScanlineSizeLimit)
            : settings.Output?.Geometry(width, height);
        var (renderWidth, renderHeight) = layout?.CrtRenderSize() ?? automatic;
        if (settings.Renderer != "legacy-v1") new OutputSettings(renderWidth, renderHeight).Validate();
        else if (settings.Crt && (renderWidth > 8192 || renderHeight > 8192 || (long)renderWidth * renderHeight > 24_000_000))
            throw new InvalidOperationException(Localization.T("CRT-Ausgabe ist zu groß. Bitte eine kleinere Eingangsbreite oder Ausgabegröße wählen."));
        return new(renderWidth, renderHeight, layout, lines);
    }

    internal static bool UsesDownscale(RenderSettings settings) => settings.Renderer == "legacy-v1" ? settings.Crt
        : settings.ImportedLook?.Downscale ?? settings.DownscaleEnabled ??
            (settings.Crt || settings.Digital != null || settings.Analog?.Active == true || settings.ScreenLoop != null);

    private static int SharedScanlines(int width, int sourceHeight, RenderSettings settings)
    {
        // Match the shared core's DownscaleSpec, including the aspect floor and single-field mode.
        var aspect = Math.Max(0.05, width / (double)sourceHeight);
        var height = Math.Max(16, 2 * (int)Math.Round(settings.RetroWidth / aspect / 2, MidpointRounding.AwayFromZero));
        var oneField = settings.ImportedLook?.OneField ??
            (settings.ScreenLoop == null && settings.Digital?.GetValueOrDefault("fields") is >= 0.5 and < 1.5);
        return oneField ? height / 2 : height;
    }
    public void Dispose() { _worker.Dispose(); lock (_gate) { _crt?.Dispose(); _crt = null; } }
}
