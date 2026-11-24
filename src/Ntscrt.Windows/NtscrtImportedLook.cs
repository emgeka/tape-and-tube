// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ntscrt.Windows;

internal sealed record NtscrtImportedLook(string Json, int RetroWidth, bool Downscale, bool Crt, int KeyframeCount,
    double Seconds, int Fps, SourceCrop? Crop = null, bool OneField = false)
{
    private const int MaxBytes = 450_000;
    public const double MaxStillSeconds = 120;

    public static NtscrtImportedLook? Parse(string text)
    {
        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        if (root.TryGetProperty("Kind", out _) || !root.TryGetProperty("downscale", out var downscale)) return null;
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes)
            throw new InvalidDataException(Localization.T("Preset ist zu groß."));

        var version = Integer(root, "version", 1);
        if (version is < 1 or > 3 || downscale.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("ntsc", out var ntsc) || ntsc.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("shader", out var shader) || shader.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        var crop = root.TryGetProperty("crop", out var cropNode) ? SourceCrop.FromNtscrt(cropNode) : null;

        var width = Integer(downscale, "width", 320);
        if (width is < 16 or > 1280) throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        var on = Flag(downscale, "enabled", true);
        var crt = Flag(shader, "enabled", true);
        var oneField = root.TryGetProperty("digital", out var digital) && digital.ValueKind == JsonValueKind.Object
            && digital.TryGetProperty("enabled", out var digitalOn) && digitalOn.ValueKind == JsonValueKind.True
            && digital.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Object
            && values.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Number
            && fields.GetDouble() is >= 0.5 and < 1.5;
        if (crt && shader.TryGetProperty("preset", out var shaderId)
            && shaderId.GetString() is not ("aperture" or "easymode" or "glow_gauss" or "glow_lanczos" or "hyllian" or "royale" or "sim"))
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        if (downscale.TryGetProperty("method", out var method)
            && !LookPreset.IsDownscaleMethod(method.GetString()))
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));

        var count = 0;
        var seconds = 5.0;
        var fps = 30;
        if (root.TryGetProperty("timeline", out var timeline))
        {
            fps = Integer(timeline, "fps", fps);
            if (timeline.TryGetProperty("duration", out var duration)) seconds = duration.GetDouble();
            if (timeline.TryGetProperty("keys", out var keys))
            {
                if (keys.GetArrayLength() > 1000 || keys.EnumerateArray().Any(key =>
                    !key.TryGetProperty("t", out var time) || !time.TryGetDouble(out var t) || t < 0 || t > 1 ||
                    (key.TryGetProperty("easing", out var easing) &&
                    easing.GetString() is not ("Linear" or "Ease in" or "Ease out" or "Ease in-out" or "Hold"))))
                    throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
                if (Flag(timeline, "enabled", keys.GetArrayLength() > 0)) count = keys.GetArrayLength();
            }
        }
        if (seconds is < 0.5 or > MaxStillSeconds || !double.IsFinite(seconds) || fps is < 1 or > 60)
            throw new InvalidDataException(Localization.T("Unbekanntes oder ungültiges Presetformat."));
        // Minify for transport. The original text is kept by the window for saving.
        if (crop == null) return new(JsonSerializer.Serialize(root), width, on, crt, count, seconds, fps, OneField: oneField);
        // The host supplies cropped source and history pixels to the worker.
        var renderLook = JsonNode.Parse(text)!.AsObject();
        renderLook.Remove("crop");
        return new(renderLook.ToJsonString(), width, on, crt, count, seconds, fps, crop, oneField);
    }

    private static int Integer(JsonElement obj, string key, int fallback) =>
        obj.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;

    private static bool Flag(JsonElement obj, string key, bool fallback) =>
        obj.TryGetProperty(key, out var value) ? value.GetBoolean() : fallback;
}
