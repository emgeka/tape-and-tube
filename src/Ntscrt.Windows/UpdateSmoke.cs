// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace Ntscrt.Windows;

internal static class UpdateSmoke
{
    internal static void Run()
    {
        var checks = new List<string>();
        void Check(bool condition, string text) { if (!condition) throw new InvalidOperationException(text); checks.Add(text); }
        int Compare(string a, string b) => ReleaseVersion.Parse(a)!.CompareTo(ReleaseVersion.Parse(b)!);
        Check(Compare("v0.1.0-beta.10", "0.1.0-beta.2") > 0, "Numeric prerelease ordering");
        Check(Compare("0.1.0", "0.1.0-rc.9") > 0, "Stable version supersedes prerelease");
        Check(Compare("0.2.0-beta.1", "0.1.0") > 0, "New minor beta ordering");
        Check(Compare("0.1.0+build.4", "0.1.0+build.9") == 0, "Build metadata does not force updates");
        Check(new[] { "1.2", "v1.2.3-01", "1.02.3", "<script>", "1.2.3-", "999999999999.0.0" }.All(v => ReleaseVersion.Parse(v) == null), "Malformed tags rejected");
        object Release(string tag, bool beta = false, bool draft = false, string? url = null) =>
            new { tag_name = tag, prerelease = beta, draft, html_url = url ?? "https://github.com/emgeka/tape-and-tube/releases/tag/" + tag };
        var feed = JsonSerializer.Serialize(new[]
        {
            Release("v0.1.0-beta.10", true), Release("v0.2.0-beta.1", true), Release("v0.1.0"),
            Release("v99.0.0", draft: true), Release("v88.0.0", url: "https://example.com/download"),
            Release("v77.0.0-beta.1"), Release("v0.0.9")
        });
        // The deliberately mislabeled beta is excluded from stable even if GitHub's flag is wrong.
        Check(UpdateService.SelectRelease(feed, "0.1.0-beta.1", UpdateChannel.Release)?.Version == "v0.1.0", "Release channel excludes all prereleases, drafts and foreign URLs");
        var normal = JsonSerializer.Serialize(new[] { Release("v0.1.0-beta.10", true), Release("v0.2.0-beta.1", true), Release("v0.1.0") });
        Check(UpdateService.SelectRelease(normal, "0.1.0-beta.1", UpdateChannel.Beta)?.Version == "v0.2.0-beta.1", "Beta channel selects newest eligible semantic version");
        Check(UpdateService.SelectRelease(JsonSerializer.Serialize(new[] { Release("v0.1.0-beta.2", true), Release("v0.1.0") }), "0.1.0-beta.1", UpdateChannel.Beta)?.Version == "v0.1.0", "Beta users also receive stable releases");
        Check(UpdateService.SelectRelease(normal, "0.1.0", UpdateChannel.Release) == null, "Equal/older versions do not trigger downloads");
        Check(UpdateService.SelectRelease(JsonSerializer.Serialize(new[] { Release("v0.0.9") }), "0.1.0-beta.1", UpdateChannel.Release) == null, "Channel switch never silently downgrades");
        Check(UpdateService.SelectRelease("[]", "0.1.0-beta.1", UpdateChannel.Beta) == null, "Empty first-publication feed handled");
        Check(new[] { "http://github.com/emgeka/tape-and-tube/releases/tag/v1.0.0", "https://github.com/other/repo/releases/tag/v1.0.0", "https://github.com.evil.example/emgeka/tape-and-tube/releases/tag/v1.0.0", "https://user@github.com/emgeka/tape-and-tube/releases/tag/v1.0.0" }.All(v => !UpdateService.TrustedPage(new Uri(v))), "Only canonical HTTPS release pages of this repository can open");
        var path = Path.Combine(Paths.Root, "artifacts", "update-fixtures", "settings.json");
        foreach (var channel in new[] { UpdateChannel.Release, UpdateChannel.Beta })
        {
            var value = new UpdatePreferences(channel, false);
            UpdateService.WritePreferences(path, value);
            Check(UpdateService.ReadPreferences(path) == value, "Channel and automatic preference survive restart: " + channel);
        }
        File.WriteAllText(path, "{broken");
        Check(UpdateService.ReadPreferences(path) == new UpdatePreferences(UpdateService.BuildChannel), "Corrupt preferences recover to build channel");
        File.WriteAllText(path, "{\"Channel\":99,\"Automatic\":false}");
        Check(UpdateService.ReadPreferences(path).Channel == UpdateService.BuildChannel, "Unknown channel rejected");
        Check(Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Length == 0, "Atomic preference writes leave no temporary files");
        Directory.CreateDirectory(Path.Combine(Paths.Root, "artifacts"));
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "update-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
