// SPDX-License-Identifier: GPL-3.0-only
using System.IO;

namespace Ntscrt.Windows;

internal static class Paths
{
    public static string Root { get; } = FindRoot();
    public static string ShaderRoot
    {
        get
        {
            var configured = Components.ConfiguredPath("shaderRoot", "TAPETUBE_SHADER_ROOT");
            if (configured != null) return Directory.Exists(configured) ? configured : throw new DirectoryNotFoundException(Localization.T("Der konfigurierte Shader-Ordner existiert nicht: ") + configured);
            var assets = Path.Combine(Root, "assets");
            var portable = Path.Combine(assets, "slang-shaders-selected");
            if (Directory.Exists(portable)) return portable;
            return Directory.Exists(assets) ? Directory.GetDirectories(assets, "slang-shaders*").OrderBy(x => x, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new DirectoryNotFoundException(Localization.T("Keine CRT-Shader gefunden. Bitte in components.local.json einen shaderRoot eintragen."))
                : throw new DirectoryNotFoundException(Localization.T("Keine CRT-Shader gefunden. Bitte in components.local.json einen shaderRoot eintragen."));
        }
    }
    private static string FindRoot()
    {
        if (File.Exists(Path.Combine(AppContext.BaseDirectory, "release.json"))) return AppContext.BaseDirectory;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "dependencies.json"))) return d.FullName;
        throw new DirectoryNotFoundException(Localization.T("Projektordner mit dependencies.json wurde nicht gefunden."));
    }
}
