// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ntscrt.Windows;

internal sealed record EffectKeyframe(double T, string Easing, string ShaderName, Dictionary<string, float> Shader,
    JsonObject Ntsc, Dictionary<string, double> Glitch, Dictionary<string, double> Digital);

internal sealed record EffectTimeline(bool Enabled, double Seconds, EffectKeyframe[] Keys, int Fps = ScreenLoopCatalog.StillFrameRate)
{
    public const int MaxKeys = 120;
    public const double DefaultSeconds = 5;
    public static readonly string[] Easings = ["Linear", "Ease in", "Ease out", "Ease in-out", "Hold"];
    public static readonly int[] StillFrameRates = [12, 24, 30, 60];

    public void Validate()
    {
        if (!double.IsFinite(Seconds) || Seconds is < 0.5 or > NtscrtImportedLook.MaxStillSeconds || !StillFrameRates.Contains(Fps) ||
            Keys == null || Keys.Length > MaxKeys || Keys.Select(key => key?.T).Distinct().Count() != Keys.Length)
            throw new InvalidDataException(Localization.T("Ungültige Keyframe-Animation."));
        foreach (var key in Keys)
        {
            if (key == null || !double.IsFinite(key.T) || key.T is < 0 or > 1 || !Easings.Contains(key.Easing)
                || string.IsNullOrWhiteSpace(key.ShaderName) || key.Shader == null || key.Ntsc == null
                || key.Glitch == null || key.Digital == null || key.Shader.Values.Any(value => !float.IsFinite(value)))
                throw new InvalidDataException(Localization.T("Ungültige Keyframe-Animation."));
            AnalogCatalog.Current.Validate(new(false, false, key.Glitch));
            DigitalCatalog.Validate(key.Digital);
        }
    }

    public JsonObject WorkerTimeline(string shaderName)
    {
        var keys = Keys.Select(key => key.ShaderName == shaderName ? key : key with { Shader = [] });
        return new() { ["keys"] = JsonSerializer.SerializeToNode(keys, CoreWorkerClient.Json) };
    }
}
