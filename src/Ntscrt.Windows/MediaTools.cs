// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class MediaTools
{
    public static string Find(string name)
    {
        var variable = Environment.GetEnvironmentVariable("TAPETUBE_" + name.ToUpperInvariant())
            ?? Environment.GetEnvironmentVariable("NTSCRT_" + name.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(variable)) return File.Exists(variable) ? Path.GetFullPath(variable) : throw new FileNotFoundException(Localization.T("Das konfigurierte Medienwerkzeug wurde nicht gefunden."), variable);
        var configured = Components.ConfiguredPath(name, "TAPETUBE_" + name.ToUpperInvariant());
        if (configured != null) return File.Exists(configured) ? configured : throw new FileNotFoundException(Localization.T("Das konfigurierte Medienwerkzeug wurde nicht gefunden."), configured);
        var bundled = Path.Combine(Paths.Root, "runtime", "media", name + ".exe");
        if (File.Exists(bundled)) return bundled;
        var configuration = Path.Combine(Paths.Root, "runtime", "media-tools.json");
        if (File.Exists(configuration))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(configuration));
            if (json.RootElement.TryGetProperty(name, out var entry) && entry.GetString() is string path && File.Exists(path)) return path;
        }
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var path = Path.Combine(directory.Trim('"'), name + ".exe");
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException(Localization.F($"{name} fehlt. Bitte FFmpeg bereitstellen und ffmpeg.exe sowie ffprobe.exe im Unterordner runtime/media ablegen."));
    }

    public static Process Start(string name, IEnumerable<string> arguments, bool output = true, bool input = false)
    {
        var info = new ProcessStartInfo(Find(name))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = output, RedirectStandardError = true, RedirectStandardInput = input
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new InvalidOperationException(Localization.F($"{name} konnte nicht gestartet werden."));
    }

    public static void Stop(Process? process)
    {
        if (process == null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    public static async Task<string> CaptureAsync(string name, IEnumerable<string> arguments, CancellationToken cancellation)
    {
        using var process = Start(name, arguments);
        using var registration = cancellation.Register(() => Stop(process));
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync(cancellation);
        try
        {
            await process.WaitForExitAsync(cancellation);
            var text = await output;
            var error = await errors;
            cancellation.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new InvalidDataException($"{name}: {error.Trim()}");
            return text;
        }
        catch { Stop(process); throw; }
    }
}
