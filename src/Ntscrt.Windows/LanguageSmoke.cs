// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Ntscrt.Windows;

internal static class LanguageSmoke
{
    public static void Run()
    {
        var checks = new List<string>();
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        Check(Localization.Resolve("auto", CultureInfo.GetCultureInfo("de-DE")) == "de", "German system language");
        Check(Localization.Resolve("auto", CultureInfo.GetCultureInfo("de-CH")) == "de", "Regional German system language");
        Check(Localization.Resolve("auto", CultureInfo.GetCultureInfo("en-GB")) == "en", "English system language");
        Check(Localization.Resolve("auto", CultureInfo.GetCultureInfo("nl-NL")) == "en", "Unsupported system language falls back to English");
        Check(Localization.Supported.Length == 10 && Localization.Supported.Select(l => l.Code).Distinct().Count() == 10, "Exactly ten distinct selectable languages");
        foreach (var (culture, expected) in new[] { ("es-MX", "es"), ("fr-CA", "fr"), ("pt-PT", "pt-BR"), ("it-IT", "it"), ("ru-RU", "ru"), ("zh-CN", "zh-Hans"), ("zh-TW", "zh-Hans"), ("ja-JP", "ja"), ("ko-KR", "ko") })
            Check(Localization.Resolve("auto", CultureInfo.GetCultureInfo(culture)) == expected, "Regional system language mapping: " + culture);
        Check(Localization.Resolve("en", CultureInfo.GetCultureInfo("de-DE")) == "en" && Localization.Resolve("de", CultureInfo.GetCultureInfo("en-US")) == "de", "Manual preference overrides system language");
        Check(Localization.Texts.Resources.Values.All(Localization.Texts.Translations.ContainsKey), "Every localized XAML resource has an English translation");
        Check(Localization.Texts.Translations.Values.All(s => !string.IsNullOrWhiteSpace(s)), "No empty translations");
        string Slots(string s) => string.Join("|", Regex.Matches(s, @"\{\d+(?:[^{}]*)\}").Select(m => m.Value).Order());
        Check(Localization.Texts.Translations.All(p => Slots(p.Key) == Slots(p.Value)), "Formatting placeholders and formats match across languages");
        var effectKeys = EffectKeys();
        var translatedLanguages = Localization.Supported.Where(l => l.Code != "en").Select(l => l.Code).ToArray();
        Check(Localization.EffectTexts.Keys.ToHashSet().SetEquals(translatedLanguages), "Effect catalogs cover all selectable translations");
        foreach (var code in translatedLanguages)
        {
            var catalog = Localization.EffectTexts[code];
            var missing = effectKeys.Where(key => !catalog.TryGetValue(key, out var translation) || string.IsNullOrWhiteSpace(translation)).ToArray();
            Check(missing.Length == 0, "Effect text coverage for " + code + (missing.Length == 0 ? "" : ": " + string.Join(" | ", missing)));
        }
        foreach (var language in Localization.Supported.Where(l => l.Code is not ("de" or "en")))
        {
            var catalog = Localization.Texts.Languages[language.Code];
            Check(catalog.Count == Localization.Texts.Translations.Count && Localization.Texts.Translations.Keys.All(k => catalog.ContainsKey(k) && !string.IsNullOrWhiteSpace(catalog[k])), "Complete translation coverage: " + language.Code);
            Check(catalog.All(p => Slots(p.Key) == Slots(p.Value)), "All formatting placeholders preserved: " + language.Code);
            Check(catalog.All(p => Regex.Match(p.Key, @"^\s*").Value == Regex.Match(p.Value, @"^\s*").Value && Regex.Match(p.Key, @"\s*$").Value == Regex.Match(p.Value, @"\s*$").Value), "Whitespace preserved for composed messages: " + language.Code);
            Check(catalog.Where(p => p.Key.Contains('|')).All(p => p.Key.Split('|').Where((_, i) => i % 2 == 1).SequenceEqual(p.Value.Split('|').Where((_, i) => i % 2 == 1))), "File dialog patterns preserved: " + language.Code);
            Localization.SetPreference(language.Code, persist: false);
            Check((string)Application.Current.Resources["LanguageLabel"] == catalog["Sprache"] && Localization.T("Bild speichern…") == catalog["Bild speichern…"], "Immediate UI switch: " + language.Code);
            Check(Localization.F($" · etwa {TimeSpan.FromSeconds(65):hh\\:mm\\:ss} verbleibend").Contains("00:01:05"), "Progress time format works: " + language.Code);
            Check(Localization.Resolve(language.Code, CultureInfo.GetCultureInfo("de-DE")) == language.Code, "Manual selection overrides system language: " + language.Code);
        }
        var originalCulture = CultureInfo.CurrentCulture;
        Localization.SetPreference("en", persist: false);
        Check(Localization.T("Bild speichern…") == "Save image…", "English application strings");
        var analogParameter = AnalogCatalog.Current.Parameters.First(p => p.Id == "signal_strength");
        var effect = new CheckBox { IsChecked = true };
        Localization.BindEffectText(effect, ContentControl.ContentProperty, analogParameter.Label);
        Localization.BindEffectText(effect, FrameworkElement.ToolTipProperty, analogParameter.Help);
        Check((string)Application.Current.Resources["LanguageLabel"] == "Language", "English live UI resources");
        Check(Localization.F($"{100.0:0}% · Bild {2} / {3} · {320} × {180}{""}") == "100% · Frame 2 / 3 · 320 × 180", "Translated progress formatting");
        Check(Localization.F($" · etwa {TimeSpan.FromSeconds(65):hh\\:mm\\:ss} verbleibend") == " · about 00:01:05 remaining", "Translated remaining-time formatting");
        var window = new Window { Content = effect, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        var shutdownMode = Application.Current.ShutdownMode;
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            window.Show();
            Check(Equals(effect.Content, "Signal strength") && Equals(effect.ToolTip, analogParameter.Help), "English effect label and tooltip");
            foreach (var code in translatedLanguages)
            {
                Localization.SetPreference(code, persist: false);
                var catalog = Localization.EffectTexts[code];
                Check(Equals(effect.Content, catalog[analogParameter.Label]) && !Equals(effect.Content, analogParameter.Label)
                    && Equals(effect.ToolTip, catalog[analogParameter.Help]) && !Equals(effect.ToolTip, analogParameter.Help)
                    && effect.IsChecked == true, "Live effect label, tooltip and state: " + code);
            }
            Localization.SetPreference("de", persist: false);
            Check(Equals(effect.Content, "Signalstärke") && effect.ToolTip is string tooltip &&
                tooltip.StartsWith("Pegel des Antennensignals.", StringComparison.Ordinal), "Existing effect label and tooltip switch to German");
        }
        finally
        {
            window.Close();
            Application.Current.ShutdownMode = shutdownMode;
        }
        Check(Localization.EffectText("Untranslated upstream text") == "Untranslated upstream text", "Untranslated upstream effect text uses its source");
        Check(Localization.T("Bild speichern…") == "Bild speichern…" && (string)Application.Current.Resources["LanguageLabel"] == "Sprache", "Switch back to German immediately");
        Check(CultureInfo.CurrentCulture.Equals(originalCulture), "Language switching preserves numeric culture and effect data conventions");
        Check(Localization.T("crt-easymode") == "crt-easymode" && Localization.T("GPL-3.0-only") == "GPL-3.0-only", "Technical names and license identifiers remain unchanged");
        var folder = Path.Combine(Paths.Root, "artifacts", "language-fixtures");
        Directory.CreateDirectory(folder);
        var workspacePath = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".workspace");
        try
        {
            Check(!MainWindow.ReadExpertMode(workspacePath), "First start opens Basic");
            MainWindow.WriteWorkspaceMode(workspacePath, true);
            Check(MainWindow.ReadExpertMode(workspacePath), "Expert preference survives restart");
            MainWindow.WriteWorkspaceMode(workspacePath, false);
            Check(File.ReadAllText(workspacePath) == "basic" && !MainWindow.ReadExpertMode(workspacePath), "Basic preference replaces Expert");
        }
        finally { if (File.Exists(workspacePath)) File.Delete(workspacePath); }
        var path = Path.Combine(folder, "preferences.json");
        foreach (var language in new[] { "auto" }.Concat(Localization.Supported.Select(l => l.Code)))
        {
            Localization.WritePreference(path, language);
            Check(Localization.ReadPreference(path) == language, "Saved language survives reload: " + language);
        }
        File.WriteAllText(path, "{broken");
        Check(Localization.ReadPreference(path) == "auto", "Corrupt settings recover to automatic selection");
        File.WriteAllText(path, "{\"Language\":\"xx\"}");
        Check(Localization.ReadPreference(path) == "auto", "Unknown saved language recovers to automatic selection");
        Check(Localization.ReadPreference(Path.Combine(folder, "missing.json")) == "auto", "First launch defaults to automatic selection");
        try { Localization.SetPreference("xx", persist: false); throw new InvalidOperationException("Unsupported preference accepted"); }
        catch (ArgumentException) { checks.Add("Invalid manual language rejected"); }
        File.WriteAllText(Path.Combine(Paths.Root, "artifacts", "language-smoke.json"), JsonSerializer.Serialize(new { passed = true, count = checks.Count, translations = Localization.Texts.Translations.Count, effectTranslations = effectKeys.Count, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static HashSet<string> EffectKeys()
    {
        var analog = AnalogCatalog.Current;
        var screenLoop = ScreenLoopCatalog.Current;
        var keys = analog.Parameters.SelectMany(p => new[] { p.Label, p.Section, p.Help }.Concat(p.Choices))
            .Concat(analog.Tapes.Concat(analog.Decks).SelectMany(p => new[] { p.Name, p.Help }))
            .Concat(screenLoop.Parameters.SelectMany(p => new[] { p.Label, p.Help }))
            .Concat(screenLoop.Presets.Select(p => p.Name))
            .Concat(DigitalCatalog.Looks.Select(p => p.Name))
            .Concat(DigitalCatalog.Parameters.SelectMany(p => new[] { p!["label"]!.GetValue<string>() }
                .Concat(p["kind"]?["choices"] is JsonArray choices ? choices.Select(c => c!.GetValue<string>()) : [])))
            .Concat(NtscKeys(NtscProcessor.Schema()));
        return keys.OfType<string>().Where(text => text.Length != 0).ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<string> NtscKeys(JsonArray descriptors)
    {
        foreach (var node in descriptors.OfType<JsonObject>())
        {
            yield return node["label"]!.GetValue<string>();
            if (node["description"]?.GetValue<string>() is { Length: > 0 } help) yield return help;
            if (node["options"] is JsonArray options)
                foreach (var option in options) yield return option!["label"]!.GetValue<string>();
            if (node["children"] is JsonArray children)
                foreach (var label in NtscKeys(children)) yield return label;
        }
    }
}
