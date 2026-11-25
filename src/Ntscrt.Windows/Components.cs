// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;

namespace Ntscrt.Windows;

internal static class Components
{
    private static readonly ConcurrentDictionary<string, string> LoadedLibraries = new();
    private static JsonObject Read(string name)
    {
        var path = Path.Combine(Paths.Root, name);
        return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path))!.AsObject() : new();
    }
    public static string? ConfiguredPath(string key, string environment)
    {
        var configured = Environment.GetEnvironmentVariable(environment);
        if (string.IsNullOrWhiteSpace(configured))
        {
            var local = Read("components.local.json");
            var shared = Read("components.json");
            configured = (local.ContainsKey(key) ? local[key] : shared[key])?.GetValue<string>();
        }
        return string.IsNullOrWhiteSpace(configured) ? null : Path.GetFullPath(configured, Paths.Root);
    }
    public static IEnumerable<string> ShaderPresets()
    {
        var local = Read("components.local.json");
        var shared = Read("components.json");
        var configured = (local.ContainsKey("shaderPresets") ? local["shaderPresets"] : shared["shaderPresets"])?.AsArray();
        if (configured == null) return new[] { "crt-easymode", "crt-aperture", "crt-hyllian", "crt-royale", "crtglow_gauss", "crtglow_lanczos", "crtsim" }
            .Select(n => $"crt/{n}.slangp");
        return configured.Select(n => n!.GetValue<string>()).ToArray();
    }
    public static Dictionary<string, string> AvailableShaderPresets()
    {
        var shaders = new Dictionary<string, string>();
        foreach (var preset in ShaderPresets())
        {
            var path = Path.GetFullPath(preset, Paths.ShaderRoot);
            var name = Path.GetFileNameWithoutExtension(path);
            if (shaders.ContainsKey(name)) name = preset.Replace('\\', '/');
            if (File.Exists(path)) shaders.TryAdd(name, path);
        }
        return shaders;
    }
    public static string NativePath(string key, string environment, string filename)
    {
        var configured = ConfiguredPath(key, environment);
        var hasExplicitOverride = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environment)) || Read("components.local.json")[key] != null;
        if (hasExplicitOverride && configured != null && !File.Exists(configured))
            throw new FileNotFoundException(Localization.T("Die konfigurierte Bibliothek wurde nicht gefunden."), configured);
        return configured != null && File.Exists(configured) ? configured : Path.Combine(AppContext.BaseDirectory, filename);
    }
    public static (string Path, bool Loaded) NativeLocation(string key, string environment, string filename) =>
        LoadedLibraries.TryGetValue(filename, out var loaded) ? (loaded, true) : (NativePath(key, environment, filename), false);

    public static string NativeStatus(string key, string environment, string filename)
    {
        var (path, loaded) = NativeLocation(key, environment, filename);
        return Localization.T(loaded ? "Geladen: " : "Vorgesehen: ") + path;
    }

    private static nint Load(string key, string environment, string filename)
    {
        var path = NativePath(key, environment, filename);
        var handle = NativeLibrary.Load(path);
        LoadedLibraries[filename] = path;
        return handle;
    }

    public static void ConfigureNativeLibraries()
    {
        NativeLibrary.SetDllImportResolver(typeof(Components).Assembly, (name, assembly, searchPath) => name switch
        {
            "ntscrt_ntsc.dll" => Load("ntscLibrary", "TAPETUBE_NTSC_LIBRARY", name),
            "librashader.dll" => Load("librashaderLibrary", "TAPETUBE_LIBRASHADER_LIBRARY", name),
            _ => 0
        });
    }
}
