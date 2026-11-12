// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace Ntscrt.Windows;

internal static class Localization
{
    internal sealed record Catalog(Dictionary<string, string> Translations, Dictionary<string, string> Resources,
        Dictionary<string, Dictionary<string, string>> Languages);
    internal sealed record LanguageOption(string Code, string NativeName);
    internal static readonly LanguageOption[] Supported =
    [
        new("de", "Deutsch"), new("en", "English"), new("es", "Español"), new("fr", "Français"),
        new("pt-BR", "Português (Brasil)"), new("it", "Italiano"), new("ru", "Русский"),
        new("zh-Hans", "简体中文"), new("ja", "日本語"), new("ko", "한국어")
    ];
    private sealed record Preferences(string Language);
    internal static readonly Catalog Texts = LoadCatalog();
    private static readonly CultureInfo SystemUiCulture = CultureInfo.CurrentUICulture;
    public static string Preference { get; private set; } = "auto";
    // The UI culture is independent of JSON, effect values, filenames and preset identifiers.
    public static string Language { get; private set; } = Resolve("auto", CultureInfo.CurrentUICulture);
    public static event Action? Changed;
    internal static string PreferencesPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TapeTube", "ui-settings.json");

    private static Catalog LoadCatalog()
    {
        using var stream = typeof(Localization).Assembly.GetManifestResourceStream("Ntscrt.Windows.Translations.json")
            ?? throw new InvalidOperationException("Missing embedded translation catalog.");
        return JsonSerializer.Deserialize<Catalog>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    public static string Resolve(string preference, CultureInfo systemCulture)
    {
        if (Supported.Any(l => l.Code == preference)) return preference;
        var code = systemCulture.TwoLetterISOLanguageName switch { "pt" => "pt-BR", "zh" => "zh-Hans", var other => other };
        return Supported.Any(l => l.Code == code) ? code : "en";
    }
    internal static string ReadPreference(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path))?.Language;
            return Supported.Any(l => l.Code == value) ? value! : "auto";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return "auto"; }
    }
    internal static void WritePreference(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new Preferences(value)));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Initialize() => SetPreference(ReadPreference(PreferencesPath), persist: false);
    public static void SetPreference(string value, bool persist = true)
    {
        if (value != "auto" && !Supported.Any(l => l.Code == value)) throw new ArgumentException("Unsupported language preference.", nameof(value));
        if (persist) WritePreference(PreferencesPath, value);
        Preference = value;
        Language = Resolve(value, SystemUiCulture);
        // Do not change CurrentCulture: native effect/preset number handling remains unchanged.
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(Language);
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.CurrentUICulture;
        foreach (var entry in Texts.Resources) Application.Current.Resources[entry.Key] = T(entry.Value);
        Application.Current.Resources["LanguageLabel"] = T("Sprache");
        Application.Current.Resources["SystemLanguage"] = T("Systemsprache");
        Changed?.Invoke();
    }
    public static string T(string german)
    {
        if (Language == "de") return german;
        if (Language != "en" && Texts.Languages.TryGetValue(Language, out var catalog) && catalog.TryGetValue(german, out var translated)) return translated;
        return Texts.Translations.GetValueOrDefault(german, german);
    }
    public static string F(FormattableString text) => string.Format(CultureInfo.CurrentCulture, T(text.Format), text.GetArguments());
}
