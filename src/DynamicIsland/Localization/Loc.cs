using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace DynamicIsland.Localization;

public sealed record LanguageInfo(string Code, string NativeName, string Culture);

/// <summary>
/// Provides translated UI strings and the culture used for dates.
///
/// Translations live in Localization/Languages/&lt;code&gt;.json and are embedded into the
/// assembly. Missing keys fall back to English, so a partial translation never breaks the UI.
///
/// XAML binds to this through the indexer (see <see cref="TExtension"/>), and raising
/// PropertyChanged("Item[]") makes every bound text update instantly when the language changes.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    /// <summary>To add a language: add a JSON file in Languages/ and one line here.</summary>
    public static IReadOnlyList<LanguageInfo> Languages { get; } = new[]
    {
        new LanguageInfo("en", "English", "en-GB"),
        new LanguageInfo("tr", "Türkçe", "tr-TR"),
        new LanguageInfo("de", "Deutsch", "de-DE"),
        new LanguageInfo("fr", "Français", "fr-FR"),
        new LanguageInfo("es", "Español", "es-ES"),
        new LanguageInfo("it", "Italiano", "it-IT"),
        new LanguageInfo("pt", "Português", "pt-PT"),
    };

    private readonly Dictionary<string, string> _fallback;
    private Dictionary<string, string> _strings;

    public string Language { get; private set; } = "en";
    public CultureInfo Culture { get; private set; } = new("en-GB");

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    private Loc()
    {
        _fallback = Load("en");
        _strings = _fallback;
    }

    public string this[string key] =>
        _strings.TryGetValue(key, out var text) ? text :
        _fallback.TryGetValue(key, out var english) ? english :
        key;

    /// <summary>Translated text for a key.</summary>
    public static string T(string key) => Instance[key];

    /// <summary>Translated text with {0}, {1}… placeholders filled in.</summary>
    public static string T(string key, params object[] args) =>
        string.Format(Instance.Culture, Instance[key], args);

    public void SetLanguage(string? code)
    {
        var info = Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
                   ?? Languages[0];

        Language = info.Code;
        Culture = new CultureInfo(info.Culture);
        _strings = info.Code == "en" ? _fallback : Load(info.Code);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Dictionary<string, string> Load(string code)
    {
        try
        {
            using Stream? stream = typeof(Loc).Assembly.GetManifestResourceStream($"Languages.{code}.json");
            if (stream != null)
                return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
        }
        catch
        {
            // A broken translation file should never crash the app.
        }
        return new();
    }
}
