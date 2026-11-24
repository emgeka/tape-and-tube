// SPDX-License-Identifier: GPL-3.0-only
using Ntscrt.Windows;

namespace TapeTube.Cli;

internal static class EffectCatalog
{
    internal static IReadOnlyList<string> Stages { get; } =
        ["processing", "ntsc", "digital", "vcr", "reception", "crt", "screen-loop"];

    public static Dictionary<string, object> Describe(string? stage, string? shader, Dictionary<string, string> shaders)
    {
        if (shader != null) stage ??= "crt";
        if (stage != null && stage != "all" && !Stages.Contains(stage, StringComparer.Ordinal))
            throw new ArgumentException("Unknown effect stage: " + stage);
        if (shader != null && (stage is not ("crt" or "all") || !shaders.ContainsKey(shader)))
            throw new ArgumentException("--shader requires a known CRT shader and the crt or all stage.");

        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var name in Stages)
        {
            if (stage != "all" && stage != name) continue;
            result.Add(name, name switch
            {
                "processing" => new
                {
                    presetFields = new
                    {
                        downscaleEnabled = new { values = new bool?[] { null, true, false }, @default = (bool?)null },
                        downscaleMethod = LookPreset.DownscaleMethods,
                        width = new { values = LookPreset.EffectWidths, @default = 320 }
                    }
                },
                "ntsc" => new { parameters = NtscProcessor.Schema(), defaults = NtscProcessor.Defaults() },
                "digital" => new { parameters = DigitalCatalog.Parameters, defaults = DigitalCatalog.Defaults, formats = DigitalCatalog.Looks },
                "vcr" => new
                {
                    parameters = AnalogCatalog.Current.Parameters.Where(p => p.Group is "Tape" or "Deck").ToArray(),
                    tapes = AnalogCatalog.Current.Tapes,
                    decks = AnalogCatalog.Current.Decks
                },
                "reception" => new { parameters = AnalogCatalog.Current.Parameters.Where(p => p.Group == "Reception").ToArray() },
                "crt" => shaders.Where(p => shader == null || p.Key == shader)
                    .ToDictionary(p => p.Key, p => CrtProcessor.DescribePreset(p.Value), StringComparer.Ordinal),
                "screen-loop" => new { parameters = ScreenLoopCatalog.Current.Parameters, presets = ScreenLoopCatalog.Current.Presets },
                _ => throw new InvalidOperationException("Unrecognized catalog stage.")
            });
        }
        return result;
    }
}
