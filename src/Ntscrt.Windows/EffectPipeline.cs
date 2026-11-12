// SPDX-License-Identifier: GPL-3.0-only
namespace Ntscrt.Windows;

internal sealed record RenderSettings(bool Ntsc, bool Crt, string NtscJson, string Shader,
    int RetroWidth, int Scale, uint Frame, Dictionary<string, float> ShaderValues, OutputSettings? Output = null);
internal sealed record RenderResult(PixelFrame Frame, IReadOnlyList<ShaderParameter> Parameters);

internal sealed class EffectPipeline : IDisposable
{
    private readonly object _gate = new();
    private CrtProcessor? _crt;
    public RenderResult Render(PixelFrame source, RenderSettings settings)
    {
        lock (_gate)
        {
            var layout = settings.Output?.Geometry(source.Width, source.Height);
            var frame = new PixelFrame(source.Width, source.Height, (byte[])source.Pixels.Clone());
            if (settings.Ntsc) NtscProcessor.Process(frame, settings.NtscJson, settings.Frame);
            if (!settings.Crt) return new(layout == null ? frame : OutputComposer.Compose(frame, layout), []);
            frame = frame.Resize(Math.Clamp(settings.RetroWidth, 64, 1280));
            var (width, height) = layout?.CrtRenderSize() ?? (checked(frame.Width * settings.Scale), checked(frame.Height * settings.Scale));
            if (width > 8192 || height > 8192 || (long)width * height > 24_000_000)
                throw new InvalidOperationException(Localization.T("CRT-Ausgabe ist zu groß. Bitte eine kleinere Eingangsbreite oder Ausgabegröße wählen."));
            _crt ??= new CrtProcessor();
            _crt.Load(settings.Shader);
            var rendered = _crt.Render(frame, width, height, settings.Frame, settings.ShaderValues);
            return new(layout == null ? rendered : OutputComposer.Compose(rendered, layout), _crt.Parameters);
        }
    }
    public void Dispose() { lock (_gate) { _crt?.Dispose(); _crt = null; } }
}
