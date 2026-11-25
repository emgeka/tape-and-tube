// SPDX-License-Identifier: GPL-3.0-only
using System.IO;

namespace Ntscrt.Windows;

internal enum BasicAnalogEffect { None, UsedTape, AntennaGhost }

internal sealed record BasicLook(string Id, string DescriptionResource, string? TitleResource = null,
    string? DigitalFormat = null, BasicAnalogEffect AnalogEffect = BasicAnalogEffect.None, string? OriginalName = null)
{
    public LookPreset CreatePreset(string shader)
    {
        var digital = DigitalFormat == null ? null : DigitalCatalog.Find(DigitalFormat);
        var analog = CreateAnalogSettings();
        return new("tape-and-tube", analog == null ? 2 : 3, digital == null, digital == null, shader,
            digital?.Width ?? 320, 3, NtscProcessor.Defaults(), [],
            Renderer: DigitalCatalog.Profile, Digital: digital?.Values, Analog: analog);
    }

    public static string ReadOriginal(string name)
    {
        using var stream = typeof(BasicLook).Assembly.GetManifestResourceStream($"Ntscrt.Windows.UpstreamLooks.{name}.json")
            ?? throw new FileNotFoundException($"NTSCRT: {name}.json");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private AnalogSettings? CreateAnalogSettings()
    {
        if (AnalogEffect == BasicAnalogEffect.None) return null;
        var catalog = AnalogCatalog.Current;
        var values = catalog.Defaults();
        if (AnalogEffect == BasicAnalogEffect.UsedTape)
        {
            foreach (var (key, value) in catalog.Tapes.Single(tape => tape.Name == "Used").Values)
                values[key] = value;
            values["head_switch"] = 1.5;
            return new(true, false, values);
        }
        values["ghost_level"] = 0.24;
        values["ghost_delay"] = 3.0;
        return new(false, true, values);
    }
}

internal static class BasicLookCatalog
{
    public static IReadOnlyList<(string Heading, BasicLook[] Looks)> Groups { get; } =
    [
        ("BasicTelevisionCategory",
        [
            new("vhs-crt", "BasicVhsDescription", "BasicVhsName"),
            new("used-vhs", "BasicUsedVhsDescription", "BasicUsedVhsName", AnalogEffect: BasicAnalogEffect.UsedTape),
            new("antenna-ghost", "BasicAntennaGhostDescription", "BasicAntennaGhostName", AnalogEffect: BasicAnalogEffect.AntennaGhost),
            new("ntscrt-clean-vhs", "BasicCleanVhsDescription", OriginalName: "Clean VHS"),
            new("ntscrt-clean-crt", "BasicCleanCrtDescription", OriginalName: "Clean CRT"),
            new("ntscrt-aperture-mask", "BasicApertureMaskDescription", OriginalName: "CRT aperture mask")
        ]),
        ("BasicCamcorderCategory",
        [
            new("minidv", "BasicMiniDvDescription", DigitalFormat: "minidv")
        ]),
        ("BasicComputerVideoCategory",
        [
            new("cd-rom", "BasicCdRomDescription", DigitalFormat: "cd_rom"),
            new("webcam-2005", "BasicWebcamDescription", DigitalFormat: "webcam")
        ])
    ];
}
