// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Ntscrt.Windows;

internal sealed record LookPreset(string Kind, int Version, bool NtscEnabled, bool CrtEnabled, string Shader,
    int Width, int Scale, JsonObject Ntsc, Dictionary<string,float> Parameters, OutputSettings? Output=null,
    string Renderer="legacy-v1", Dictionary<string,double>? Digital=null, AnalogSettings? Analog=null, EffectTimeline? Timeline=null,
    SourceCrop? Crop=null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DownscaleMethod = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? DownscaleEnabled = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Baird = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] HistoricalDisplay Display = HistoricalDisplay.None,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool Film = false)
{
    internal static IReadOnlyList<string> DownscaleMethods { get; } = ["nearest", "nearest+", "bilinear", "bicubic", "lanczos", "area"];
    internal static IReadOnlyList<int> EffectWidths { get; } = [240, 256, 320, 480, 640, 720, 1280];
    internal static bool IsDownscaleMethod(string? method) => method != null && DownscaleMethods.Contains(method, StringComparer.Ordinal);

    public static LookPreset Parse(string json,IEnumerable<string> shaders)
    {
        var p=JsonSerializer.Deserialize<LookPreset>(json)??throw new InvalidDataException(Localization.T("Leeres Preset."));
        if((p.Kind!="ntscrt-windows"&&p.Kind!="tape-and-tube")||(p.Version<1||p.Version>5)||!shaders.Contains(p.Shader)
            ||p.Ntsc==null||p.Parameters==null||!(p.Version==1?new[]{256,320,640,1280}:EffectWidths).Contains(p.Width)
            ||p.Scale<2||p.Scale>4||p.Parameters.Values.Any(v=>!float.IsFinite(v)) || !Enum.IsDefined(p.Display))
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        if((p.Version is 2 or 3 && p.Renderer!=DigitalCatalog.Profile)
            || (p.Version>=4 && p.Renderer is not ("legacy-v1" or DigitalCatalog.Profile)))
            throw new InvalidDataException("Unsupported renderer profile.");
        if((p.Version==1 || p.Renderer=="legacy-v1") && (p.Digital!=null || p.Analog?.Active == true))
            throw new InvalidDataException("Digital video, VCR and reception require the shared renderer.");
        if (p.Version < 3 && p.Analog != null) throw new InvalidDataException("VCR and TV reception require preset format 3.");
        if (p.Version == 3 && p.Analog == null) throw new InvalidDataException("Missing VCR and TV reception settings.");
        if ((p.Version == 4 && p.Timeline == null) || (p.Version < 4 && p.Timeline != null))
            throw new InvalidDataException(Localization.T("Ungültige Keyframe-Animation."));
        if ((p.Version == 5) != (p.Crop != null))
            throw new InvalidDataException(Localization.T("Ungültiger Quellbeschnitt."));
        if (p.DownscaleMethod != null && !IsDownscaleMethod(p.DownscaleMethod))
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        p.Crop?.Validate();
        p.Timeline?.Validate();
        if(p.Digital!=null)DigitalCatalog.Validate(p.Digital);
        if (p.Analog != null) AnalogCatalog.Current.Validate(p.Analog);
        p.Output?.Validate();
        if (p.Baird && p.Display == HistoricalDisplay.None) p = p with { Display = HistoricalDisplay.Baird30, Baird = false };
        if (p.Display == HistoricalDisplay.Kinescope) p = p with { Display = HistoricalDisplay.None, Film = true };
        return p.Version==1?p with{Renderer="legacy-v1"}:p;
    }
}
