// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class Smoke
{
    public static void Run()
    {
        var directory = Path.Combine(Paths.Root, "artifacts"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "smoke-error.txt"), "");
        var source = PixelFrame.TestPattern(); source.Save(Path.Combine(directory, "source.png"));
        var json = NtscProcessor.Defaults().ToJsonString();
        var shader = Path.Combine(Paths.ShaderRoot, "crt", "crt-easymode.slangp");
        using var pipeline = new EffectPipeline();
        var settings = new RenderSettings(false, false, json, shader, 320, 3, 0, []);
        var bypass = pipeline.Render(source, settings).Frame;
        Require(source.Pixels.SequenceEqual(bypass.Pixels), "Bypass verändert Pixel");
        var ntsc = pipeline.Render(source, settings with { Ntsc = true }).Frame;
        Require(!source.Pixels.SequenceEqual(ntsc.Pixels), "ntsc-rs verändert das Testbild nicht");
        var repeat = pipeline.Render(source, settings with { Ntsc = true }).Frame;
        Require(ntsc.Pixels.SequenceEqual(repeat.Pixels), "ntsc-rs ist bei festem Frame nicht deterministisch");
        ntsc.Save(Path.Combine(directory, "ntsc.png"));
        var crt = pipeline.Render(source, settings with { Crt = true });
        Require(crt.Frame.Width == 960 && crt.Frame.Height == 720, "Falsche CRT-Ausgabegröße");
        Require(crt.Parameters.Count > 0, "CRT-Parameter fehlen");
        Require(crt.Frame.Pixels.Where((_, i) => i % 4 != 3).Distinct().Count() > 32, "CRT-Ausgabe ist leer");
        crt.Frame.Save(Path.Combine(directory, "crt.png"));
        var combined = pipeline.Render(source, settings with { Ntsc = true, Crt = true }).Frame;
        Require(!combined.Pixels.SequenceEqual(crt.Frame.Pixels), "NTSC-Stufe fehlt in der kombinierten Kette");
        combined.Save(Path.Combine(directory, "combined.png"));
        Require(PixelFrame.Read(Path.Combine(directory, "combined.png")).Pixels.SequenceEqual(combined.Pixels), "PNG-Roundtrip verändert Pixel");
        var rejection = false;
        try { NtscProcessor.Process(source, "not-json", 0); } catch (InvalidOperationException) { rejection = true; }
        Require(rejection, "Ungültiges NTSC-JSON wird akzeptiert");
        var shaderChecks = new List<object>();
        foreach (var name in new[] { "crt-easymode", "crt-aperture", "crt-hyllian", "crt-royale", "crtglow_gauss", "crtglow_lanczos", "crtsim" })
        {
            var result = pipeline.Render(source, settings with { Crt = true, Shader = Path.Combine(Paths.ShaderRoot, "crt", name + ".slangp") });
            Require(result.Frame.Pixels.Where((_, i) => i % 4 != 3).Distinct().Count() > 32, $"{name}: leere Ausgabe");
            shaderChecks.Add(new { name, parameters = result.Parameters.Count });
        }
        File.WriteAllText(Path.Combine(directory, "smoke.json"), JsonSerializer.Serialize(new
        {
            passed = true, checks = 16, backend = "Direct3D 11 hardware", shader, shaderChecks,
            ntscLibrary = Components.NativeStatus("ntscLibrary", "TAPETUBE_NTSC_LIBRARY", "ntscrt_ntsc.dll"),
            librashaderLibrary = Components.NativeStatus("librashaderLibrary", "TAPETUBE_LIBRASHADER_LIBRARY", "librashader.dll"),
            width = combined.Width, height = combined.Height, parameters = crt.Parameters.Count,
            sha256 = Convert.ToHexString(SHA256.HashData(combined.Pixels))
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
