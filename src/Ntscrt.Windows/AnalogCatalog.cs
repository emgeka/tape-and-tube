// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ntscrt.Windows;

internal sealed record AnalogSettings(bool Vcr, bool Reception, Dictionary<string, double> Values)
{
    [JsonIgnore]
    public bool Active => Vcr || Reception;
    public AnalogSettings Snapshot() => this with { Values = new(Values) };
}

internal sealed record AnalogParameter(string Id, string Label, string Group, string? Section, string Kind,
    double Min, double Max, double Default, string[] Choices, bool Percent, string Unit, string Help);
internal sealed record AnalogCondition(string Name, string Help, Dictionary<string, double> Values);

internal sealed record AnalogCatalog(AnalogParameter[] Parameters, AnalogCondition[] Tapes, AnalogCondition[] Decks)
{
    public static AnalogCatalog Current { get; } = Load();

    private static AnalogCatalog Load()
    {
        using var source = typeof(AnalogCatalog).Assembly.GetManifestResourceStream("Ntscrt.Windows.AnalogCatalog.json")
            ?? throw new InvalidOperationException("Missing VCR and TV reception catalog.");
        return JsonSerializer.Deserialize<AnalogCatalog>(source, CoreWorkerClient.Json)
            ?? throw new InvalidOperationException("Invalid VCR and TV reception catalog.");
    }

    public Dictionary<string, double> Defaults() => Parameters.ToDictionary(p => p.Id, p => p.Default);

    public void Validate(AnalogSettings settings)
    {
        if (settings.Values == null || settings.Values.Count != Parameters.Length)
            throw new ArgumentException("The preset must contain all VCR and TV reception parameters.");
        foreach (var parameter in Parameters)
        {
            if (!settings.Values.TryGetValue(parameter.Id, out var value) || !double.IsFinite(value)
                || value < parameter.Min || value > parameter.Max || (parameter.Kind != "slider" && value != Math.Truncate(value)))
                throw new ArgumentException("Invalid VCR or TV reception value: " + parameter.Id);
        }
    }
}
