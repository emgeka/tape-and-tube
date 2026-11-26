// SPDX-License-Identifier: GPL-3.0-only
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ntscrt.Windows;

internal sealed record DigitalLook(string Id, string Name, int Width, Dictionary<string,double> Values);
internal static class DigitalCatalog
{
    public const string Revision = "889256dde173a89e475919be3d1bba34e6cc7355";
    public const string Profile = "ntscrt-wgpu-v2";
    private static readonly JsonObject Data = Load();
    private static JsonObject Load()
    {
        using var stream=typeof(DigitalCatalog).Assembly.GetManifestResourceStream("Ntscrt.Windows.DigitalCatalog.json")
            ?? throw new InvalidOperationException("Missing digital video catalog.");
        return JsonNode.Parse(stream)!.AsObject();
    }
    public static JsonArray Parameters => Data["parameters"]!.AsArray();
    public static Dictionary<string,double> Defaults => JsonSerializer.Deserialize<Dictionary<string,double>>(Data["defaults"]!.ToJsonString())!;
    public static IReadOnlyList<DigitalLook> Looks => Data["formats"]!.AsArray().Select(f=>new DigitalLook(f!["id"]!.ToString(),f["name"]!.ToString(),f["suggestedRetroWidth"]!.GetValue<int>(),JsonSerializer.Deserialize<Dictionary<string,double>>(f["values"]!.ToJsonString())!)).ToArray();
    public static DigitalLook Find(string id) => Looks.FirstOrDefault(l=>l.Id==id) ?? throw new ArgumentException("Unknown digital look.");
    public static void Validate(Dictionary<string,double> values)
    {
        if(values.Count!=Parameters.Count)throw new ArgumentException("Digital looks must contain all parameter values.");
        foreach(var parameter in Parameters)
        {
            var id=parameter!["id"]!.ToString();
            if(!values.TryGetValue(id,out var value)||!double.IsFinite(value))throw new ArgumentException("Invalid digital setting: "+id);
            var kind=parameter["kind"]!;
            if(kind["type"]!.ToString()=="choice")
            { if(value<0||value>=kind["choices"]!.AsArray().Count||value!=Math.Round(value))throw new ArgumentException("Invalid digital choice: "+id); }
            else
            {
                var min=kind["minimum"]!.GetValue<double>();var max=kind["maximum"]!.GetValue<double>();
                if(value<min||value>max)throw new ArgumentException("Digital setting outside range: "+id);
                if(kind["step"] is JsonValue stepNode&&stepNode.TryGetValue<double>(out var step)&&step>0&&Math.Abs((value-min)/step-Math.Round((value-min)/step))>1e-6)throw new ArgumentException("Invalid digital step: "+id);
            }
        }
    }
}
