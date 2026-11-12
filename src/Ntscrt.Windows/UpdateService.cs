// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Ntscrt.Windows;

[JsonConverter(typeof(JsonStringEnumConverter<UpdateChannel>))]
internal enum UpdateChannel { Release, Beta }
internal sealed record UpdatePreferences(UpdateChannel Channel, bool Automatic = true);
internal sealed record UpdateRelease(string Version, Uri Page);

internal sealed record ReleaseVersion(int Major, int Minor, int Patch, string[] Pre) : IComparable<ReleaseVersion>
{
    internal static ReleaseVersion? Parse(string value)
    {
        var m = Regex.Match(value, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$");
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var major) || !int.TryParse(m.Groups[2].Value, out var minor) || !int.TryParse(m.Groups[3].Value, out var patch)) return null;
        var pre = m.Groups[4].Success ? m.Groups[4].Value.Split('.') : [];
        if (pre.Any(p => p.Length > 1 && p[0] == '0' && p.All(char.IsAsciiDigit))) return null;
        return new(major, minor, patch, pre);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other == null) return 1;
        foreach (var pair in new[] { (Major, other.Major), (Minor, other.Minor), (Patch, other.Patch) })
            if (pair.Item1 != pair.Item2) return pair.Item1.CompareTo(pair.Item2);
        if (Pre.Length == 0 || other.Pre.Length == 0) return (Pre.Length == 0 ? 1 : 0).CompareTo(other.Pre.Length == 0 ? 1 : 0);
        for (int i = 0; i < Math.Min(Pre.Length, other.Pre.Length); i++)
        {
            bool a = Pre[i].All(char.IsAsciiDigit), b = other.Pre[i].All(char.IsAsciiDigit);
            int result = a && b ? Pre[i].Length.CompareTo(other.Pre[i].Length) : a != b ? (a ? -1 : 1) : 0;
            if (result == 0) result = string.CompareOrdinal(Pre[i], other.Pre[i]);
            if (result != 0) return result;
        }
        return Pre.Length.CompareTo(other.Pre.Length);
    }
}

internal static class UpdateService
{
    internal const string Repository = "emgeka/tape-and-tube";
    internal static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TapeTube", "update-settings.json");
    internal static string CurrentVersion
    {
        get
        {
            var path = Path.Combine(Paths.Root, "release.json");
            if (File.Exists(path))
            {
                using var data = JsonDocument.Parse(File.ReadAllText(path));
                var version = data.RootElement.GetProperty("version").GetString();
                if (version != null && ReleaseVersion.Parse(version) != null) return version;
            }
            return "0.1.0-beta.1";
        }
    }
    internal static UpdateChannel BuildChannel => ReleaseVersion.Parse(CurrentVersion)!.Pre.Length > 0 ? UpdateChannel.Beta : UpdateChannel.Release;
    internal static UpdatePreferences ReadPreferences(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(path));
            if (value != null && Enum.IsDefined(value.Channel)) return value;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new(BuildChannel);
    }
    internal static void WritePreferences(string path, UpdatePreferences value)
    {
        if (!Enum.IsDefined(value.Channel)) throw new ArgumentException("Invalid update channel.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static bool TrustedPage(Uri uri) => uri.Scheme == "https" && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort && uri.UserInfo.Length == 0 && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/tag/", StringComparison.Ordinal)
        && uri.AbsolutePath.Length > ("/" + Repository + "/releases/tag/").Length;

    internal static UpdateRelease? SelectRelease(string json, string current, UpdateChannel channel)
    {
        var installed = ReleaseVersion.Parse(current) ?? throw new ArgumentException("Invalid installed version.");
        if (!Enum.IsDefined(channel)) throw new ArgumentException("Invalid channel.");
        using var data = JsonDocument.Parse(json);
        UpdateRelease? selected = null; ReleaseVersion? best = null;
        foreach (var release in data.RootElement.EnumerateArray())
        {
            if (release.GetProperty("draft").GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var version = ReleaseVersion.Parse(tag);
            if (version == null || version.CompareTo(installed) <= 0) continue;
            if (channel == UpdateChannel.Release && (release.GetProperty("prerelease").GetBoolean() || version.Pre.Length != 0)) continue;
            if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var page) || !TrustedPage(page)) continue;
            if (best == null || version.CompareTo(best) > 0) { selected = new(tag, page); best = version; }
        }
        return selected;
    }
    internal static async Task<UpdateRelease?> CheckAsync(UpdateChannel channel, CancellationToken cancellation)
    {
        // No account, token, telemetry payload or local settings are sent.
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TapeTube/" + CurrentVersion);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        using var response = await client.GetAsync("https://api.github.com/repos/" + Repository + "/releases?per_page=100", cancellation).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound) return null; // First publication not present yet.
        response.EnsureSuccessStatusCode();
        return SelectRelease(await response.Content.ReadAsStringAsync(cancellation).ConfigureAwait(false), CurrentVersion, channel);
    }
}
