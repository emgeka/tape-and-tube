// SPDX-License-Identifier: GPL-3.0-only
using System.IO;

namespace Ntscrt.Windows;

internal enum BasicAnalogEffect { None, UsedTape, AntennaGhost }

internal sealed record BasicLook(string Id, string DescriptionResource, string? TitleResource = null,
    string? DigitalFormat = null, BasicAnalogEffect AnalogEffect = BasicAnalogEffect.None, string? OriginalName = null,
    HistoricalDisplay Display = HistoricalDisplay.None, bool Film = false)
{
    public LookPreset CreatePreset(string shader)
    {
        var digital = DigitalFormat == null ? null : DigitalCatalog.Find(DigitalFormat);
        var analog = CreateAnalogSettings();
        var useTelevisionChain = digital == null && Display == HistoricalDisplay.None && !Film;
        return new("tape-and-tube", analog == null ? 2 : 3, useTelevisionChain, useTelevisionChain, shader,
            digital?.Width ?? 320, 3, NtscProcessor.Defaults(), [],
            Renderer: DigitalCatalog.Profile, Digital: digital?.Values, Analog: analog, Display: Display, Film: Film);
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
        ("BasicEarlyTelevisionCategory",
        [
            new("baird-30", "BairdDescription", "BairdName", Display: HistoricalDisplay.Baird30),
            new("british-405", "British405Description", "British405Name", Display: HistoricalDisplay.British405),
            new("kinescope", "KinescopeDescription", "KinescopeName", Film: true)
        ]),
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
