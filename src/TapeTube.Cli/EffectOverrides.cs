// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal sealed class EffectOverrides
{
    internal sealed record ProcessingOptions(string? Mode, int? Width, string? Method);
    internal sealed record NtscOptions(bool? Enabled, Dictionary<string, JsonElement>? Values);
    internal sealed record DigitalOptions(bool? Enabled, string? Format, Dictionary<string, double>? Values);
    internal sealed record CrtOptions(bool? Enabled, string? Shader, Dictionary<string, double>? Values);
    internal sealed record StageOptions(bool? Enabled, Dictionary<string, double>? Values);

    public ProcessingOptions? Processing { get; init; }
    public NtscOptions? Ntsc { get; init; }
    public DigitalOptions? Digital { get; init; }
    public StageOptions? Vcr { get; init; }
    public StageOptions? Reception { get; init; }
    public CrtOptions? Crt { get; init; }
    public StageOptions? ScreenLoop { get; init; }

    private static readonly JsonSerializerOptions Json = new(CoreWorkerClient.Json)
        { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    internal static RenderSettings Apply(string? json, RenderSettings settings)
    {
        if (json == null) return settings;
        if (json.Length > 100_000) throw new ArgumentException("The effects object exceeds 100,000 characters.");
        EffectOverrides options;
        try { options = JsonSerializer.Deserialize<EffectOverrides>(json, Json) ?? throw new ArgumentException("effects must be a JSON object."); }
        catch (JsonException ex) { throw new ArgumentException($"Invalid effects object at {ex.Path ?? "$"}.", ex); }
        if (options is { Processing: null, Ntsc: null, Digital: null, Vcr: null, Reception: null, Crt: null, ScreenLoop: null })
            return settings;
        if (settings.ImportedLook != null)
            throw new ArgumentException("Direct effects edits require a Tape & Tube or Screen Loop preset, or no preset. Imported NTSCRT looks are read-only.");
        return options.Apply(settings);
    }

    private RenderSettings Apply(RenderSettings settings)
    {
        // The worker receives a private copy. Keyframes for parameters not overridden remain available.
        settings = settings with { EffectTimeline = settings.EffectTimeline?.DeepClone().AsObject() };
        if (Ntsc != null) settings = ApplyNtsc(settings, Ntsc);
        if (Digital != null) settings = ApplyDigital(settings, Digital);
        if (Vcr != null || Reception != null) settings = ApplyAnalog(settings);
        if (Crt != null) settings = ApplyCrt(settings, Crt);
        if (ScreenLoop != null) settings = ApplyScreenLoop(settings, ScreenLoop);
        if (Processing != null) settings = ApplyProcessing(settings, Processing);
        return settings.ScreenLoop == null ? settings : settings with { EffectTimeline = null };
    }

    private static RenderSettings ApplyProcessing(RenderSettings settings, ProcessingOptions options)
    {
        if (options.Width is < 16 or > 1280) throw new ArgumentException("processing.width must be 16–1280.");
        if (options.Method != null && options.Method != "auto" && !LookPreset.IsDownscaleMethod(options.Method))
            throw new ArgumentException("Unknown processing.method.");
        return settings with
        {
            Renderer = DigitalCatalog.Profile,
            RetroWidth = options.Width ?? settings.RetroWidth,
            DownscaleMethod = options.Method == "auto" ? null : options.Method ?? settings.DownscaleMethod,
            DownscaleEnabled = options.Mode switch
            {
                null => settings.DownscaleEnabled,
                "auto" => null,
                "on" => true,
                "off" => false,
                _ => throw new ArgumentException("processing.mode must be auto, on or off.")
            }
        };
    }

    private static RenderSettings ApplyNtsc(RenderSettings settings, NtscOptions options)
    {
        var enabled = options.Enabled ?? (settings.Ntsc || options.Values?.Count > 0);
        if (options.Values is not { Count: > 0 } edits) return settings with { Ntsc = enabled };
        var values = NtscProcessor.Defaults();
        foreach (var (key, value) in JsonNode.Parse(settings.NtscJson)!.AsObject())
            values[key] = value?.DeepClone();
        var descriptors = NtscDescriptors(NtscProcessor.Schema()).ToDictionary(node => node["id"]!.ToString());
        foreach (var (key, value) in edits)
        {
            if (!descriptors.TryGetValue(key, out var descriptor)) throw new ArgumentException($"Unknown NTSC parameter {key}.");
            var valid = descriptor["kind"]!.ToString() switch
            {
                "bool" or "group" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i)
                    && i >= descriptor["min"]!.GetValue<int>() && i <= descriptor["max"]!.GetValue<int>(),
                "number" => value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n)
                    && n >= descriptor["min"]!.GetValue<double>() && n <= descriptor["max"]!.GetValue<double>(),
                "enum" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var choice)
                    && descriptor["options"]!.AsArray().Any(o => o!["value"]!.GetValue<int>() == choice),
                _ => false
            };
            if (!valid) throw new ArgumentException($"Invalid value for NTSC parameter {key}. See describe ntsc.");
            values[key] = JsonNode.Parse(value.GetRawText());
        }
        PinKeyframes(settings, "ntsc", JsonSerializer.SerializeToNode(edits)!.AsObject());
        return settings with { Ntsc = enabled, NtscJson = values.ToJsonString() };
    }

    private static IEnumerable<JsonNode> NtscDescriptors(JsonArray descriptors)
    {
        foreach (var node in descriptors)
        {
            yield return node!;
            if (node!["children"] is JsonArray children)
                foreach (var child in NtscDescriptors(children)) yield return child;
        }
    }

    private static RenderSettings ApplyDigital(RenderSettings settings, DigitalOptions options)
    {
        var format = options.Format == null ? null : DigitalCatalog.Find(options.Format);
        var values = new Dictionary<string, double>(format?.Values ?? settings.Digital ?? DigitalCatalog.Defaults);
        ReplaceValues(values, options.Values, "Digital video");
        DigitalCatalog.Validate(values);
        var enabled = options.Enabled ?? (settings.Digital != null || format != null || options.Values?.Count > 0);
        PinKeyframes(settings, "digital", NumericEdits(options.Values));
        return settings with { Digital = enabled ? values : null, RetroWidth = format?.Width ?? settings.RetroWidth,
            Renderer = enabled ? DigitalCatalog.Profile : settings.Renderer };
    }

    private RenderSettings ApplyAnalog(RenderSettings settings)
    {
        var previous = settings.Analog;
        var values = previous == null ? AnalogCatalog.Current.Defaults() : new(previous.Values);
        var edits = new JsonObject();
        void ApplyGroup(StageOptions? stage, bool reception)
        {
            if (stage?.Values == null) return;
            var allowed = AnalogCatalog.Current.Parameters.Where(p => (p.Group == "Reception") == reception)
                .Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var (key, value) in stage.Values)
            {
                if (!allowed.Contains(key)) throw new ArgumentException($"Unknown {(reception ? "reception" : "VCR")} parameter {key}.");
                values[key] = value;
                edits[key] = value;
            }
        }
        ApplyGroup(Vcr, false);
        ApplyGroup(Reception, true);
        var analog = new AnalogSettings(Vcr?.Enabled ?? (previous?.Vcr == true || Vcr?.Values?.Count > 0),
            Reception?.Enabled ?? (previous?.Reception == true || Reception?.Values?.Count > 0), values);
        AnalogCatalog.Current.Validate(analog);
        PinKeyframes(settings, "glitch", edits);
        return settings with { Analog = analog, Renderer = analog.Active ? DigitalCatalog.Profile : settings.Renderer };
    }

    private static RenderSettings ApplyCrt(RenderSettings settings, CrtOptions options)
    {
        var enabled = options.Enabled ?? (settings.Crt || options.Shader != null || options.Values?.Count > 0);
        if (!enabled && options.Shader == null && options.Values is not { Count: > 0 }) return settings with { Crt = false };
        var shaders = Components.AvailableShaderPresets();
        var shader = settings.Shader;
        if (options.Shader != null && !shaders.TryGetValue(options.Shader, out shader))
            throw new ArgumentException("Unknown CRT shader. See describe.");
        if (string.IsNullOrEmpty(shader))
            shader = shaders.GetValueOrDefault("crtsim") ?? shaders.Values.FirstOrDefault()
                ?? throw new ArgumentException("No CRT shader is available.");
        var changed = shader != settings.Shader;
        var values = changed ? new Dictionary<string, float>() : new(settings.ShaderValues);
        if (options.Values is { Count: > 0 } edits)
        {
            var parameters = CrtProcessor.DescribePreset(shader).ToDictionary(p => p.Name);
            foreach (var (key, value) in edits)
            {
                if (!parameters.TryGetValue(key, out var parameter) || !double.IsFinite(value)
                    || (float)value < parameter.Minimum || (float)value > parameter.Maximum)
                    throw new ArgumentException($"Invalid CRT parameter {key}. See describe crt.");
                values[key] = (float)value;
            }
        }
        PinKeyframes(settings, "shader", NumericEdits(options.Values), replace: changed);
        return settings with { Crt = enabled, Shader = shader, ShaderValues = values };
    }

    private static RenderSettings ApplyScreenLoop(RenderSettings settings, StageOptions options)
    {
        var values = new Dictionary<string, double>(settings.ScreenLoop ?? ScreenLoopCatalog.Current.Defaults());
        ReplaceValues(values, options.Values, "Screen Loop");
        ScreenLoopCatalog.Current.Validate(values);
        var enabled = options.Enabled ?? (settings.ScreenLoop != null || options.Values?.Count > 0);
        return settings with { ScreenLoop = enabled ? values : null,
            Renderer = enabled ? DigitalCatalog.Profile : settings.Renderer };
    }

    private static void ReplaceValues(Dictionary<string, double> values, Dictionary<string, double>? edits, string stage)
    {
        if (edits == null) return;
        foreach (var (key, value) in edits)
        {
            if (!values.ContainsKey(key)) throw new ArgumentException($"Unknown {stage} parameter {key}.");
            values[key] = value;
        }
    }

    private static JsonObject NumericEdits(Dictionary<string, double>? edits) =>
        JsonSerializer.SerializeToNode(edits) as JsonObject ?? new();

    private static void PinKeyframes(RenderSettings settings, string group, JsonObject edits, bool replace = false)
    {
        if (settings.EffectTimeline?["keys"] is not JsonArray keys || (!replace && edits.Count == 0)) return;
        foreach (var entry in keys)
        {
            var key = entry!.AsObject();
            var values = !replace ? key[group] as JsonObject : null;
            if (values == null) key[group] = values = new();
            foreach (var (name, value) in edits) values[name] = value?.DeepClone();
        }
    }
}
