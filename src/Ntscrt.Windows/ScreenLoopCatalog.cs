// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal sealed record ScreenLoopControl(string Id, string Label, string Group, string Kind,
    double Min, double Max, bool Percent, string Unit, double? Step, double Default, string Help);
internal sealed record ScreenLoopEntry(string Name, ScreenLoopPreset Preset);
internal sealed record ScreenLoopPreset(string Kind, int Version, Dictionary<string, double> Values, double? Seconds = null)
{
    public const string PresetKind = "ntscrt-video-feedback";
    public static ScreenLoopPreset Parse(string json)
    {
        var preset = JsonSerializer.Deserialize<ScreenLoopPreset>(json, CoreWorkerClient.Json)
            ?? throw new InvalidDataException(Localization.T("Ungültiges Screen-Loop-Preset."));
        if (preset.Kind != PresetKind || preset.Version != 2 || preset.Values == null
            || (preset.Seconds is double seconds && (!double.IsFinite(seconds) || seconds < 0.5 || seconds > 60)))
            throw new InvalidDataException(Localization.T("Ungültiges Screen-Loop-Preset."));
        var values = ScreenLoopCatalog.Current.Defaults();
        foreach (var (key, value) in preset.Values) values[key] = value;
        ScreenLoopCatalog.Current.Validate(values);
        return preset with { Values = values };
    }
}

internal sealed record ScreenLoopCatalog(ScreenLoopControl[] Parameters, ScreenLoopEntry[] Presets)
{
    public const int StillFrameRate = 30;
    public static ScreenLoopCatalog Current { get; } = Load();

    private static ScreenLoopCatalog Load()
    {
        using var stream = typeof(ScreenLoopCatalog).Assembly.GetManifestResourceStream("Ntscrt.Windows.ScreenLoopCatalog.json")
            ?? throw new InvalidOperationException("Missing Screen Loop catalog.");
        return JsonSerializer.Deserialize<ScreenLoopCatalog>(stream, CoreWorkerClient.Json)
            ?? throw new InvalidOperationException("Invalid Screen Loop catalog.");
    }

    public Dictionary<string, double> Defaults() => Parameters.ToDictionary(p => p.Id, p => p.Default);

    public void Validate(Dictionary<string, double> values)
    {
        if (values.Count != Parameters.Length) throw new InvalidDataException(Localization.T("Ungültiges Screen-Loop-Preset."));
        foreach (var parameter in Parameters)
            if (!values.TryGetValue(parameter.Id, out var value) || !double.IsFinite(value) || value < parameter.Min ||
                value > parameter.Max || (parameter.Step == 1 && value != Math.Truncate(value)))
                throw new InvalidDataException(Localization.T("Ungültiges Screen-Loop-Preset."));
    }

    public static VideoInfo StillTimeline(PixelFrame frame, double seconds, int fps = StillFrameRate) =>
        StillTimeline(frame.Width, frame.Height, seconds, fps);

    internal static VideoInfo StillTimeline(int width, int height, double seconds, int fps) =>
        new("", width, height, fps, FormattableString.Invariant($"{fps}/1"),
            Math.Max(1, (int)Math.Round(seconds * fps, MidpointRounding.AwayFromZero)), 0, IsStill: true);
}
