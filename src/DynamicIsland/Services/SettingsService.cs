using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace DynamicIsland.Services;

public sealed class AppSettings
{
    /// <summary>UI language: en, tr, de, fr, es, it, pt. Can also be changed from the ⋯ menu.</summary>
    public string Language { get; set; } = "en";

    public WeatherSettings Weather { get; set; } = new();
    public LyricsSettings Lyrics { get; set; } = new();
    public MailSettings Mail { get; set; } = new();
    public CalendarSettings Calendar { get; set; } = new();
    public ClaudeCodeSettings ClaudeCode { get; set; } = new();
    public VsCodeSettings VsCode { get; set; } = new();
}

public sealed class WeatherSettings
{
    public double Latitude { get; set; } = 41.20;
    public double Longitude { get; set; } = 32.62;
    public string LocationName { get; set; } = "Karabük";
}

public sealed class LyricsSettings
{
    /// <summary>Fetch time-synced lyrics from lrclib.net for the playing song.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Show the current lyric line in the collapsed island while music plays.</summary>
    public bool ShowInCompactIsland { get; set; } = true;

    /// <summary>Timing correction in milliseconds. Positive = lines appear later, negative = earlier.</summary>
    public int OffsetMs { get; set; } = 0;
}

public sealed class MailSettings
{
    /// <summary>The provider's IMAP server, e.g. imap.gmail.com (not your email address).</summary>
    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = "";
    /// <summary>An app password generated for this app, never your normal account password.</summary>
    public string AppPassword { get; set; } = "";
    public string WebUrl { get; set; } = "https://mail.google.com";

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(AppPassword);
}

public sealed class CalendarSettings
{
    /// <summary>Google Calendar → Settings → Integrate calendar → "Secret address in iCal format".</summary>
    public string IcsUrl { get; set; } = "";
    public string WebUrl { get; set; } = "https://calendar.google.com";
}

public sealed class ClaudeCodeSettings
{
    /// <summary>Extra project folders shown in addition to the ones detected automatically.</summary>
    public List<string> Projects { get; set; } = new();
}

public sealed class VsCodeSettings
{
    /// <summary>Path to Code.exe. Leave null to detect it automatically.</summary>
    public string? CodePath { get; set; }
    public List<string> ExtraFolders { get; set; } = new();
}

/// <summary>
/// Settings are stored in %AppData%\DynamicIsland\settings.json.
/// The file is created with default values on first run.
/// </summary>
public static class SettingsService
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DynamicIsland");

    public static string FilePath => Path.Combine(Folder, "settings.json");

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // Keep non-ASCII characters (ü, é, ç…) readable in the file
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static AppSettings Load()
    {
        try
        {
            Directory.CreateDirectory(Folder);

            if (!File.Exists(FilePath))
            {
                var defaults = new AppSettings();
                File.WriteAllText(FilePath, JsonSerializer.Serialize(defaults, Options));
                return defaults;
            }

            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings();
        }
        catch
        {
            // A broken file must not crash the app; fall back to defaults (the file is left untouched)
            return new AppSettings();
        }
    }

    /// <summary>
    /// Updates only the "Language" value and leaves everything else in the file as it is.
    /// If the file can't be parsed, nothing is written, so the user's settings are never lost.
    /// </summary>
    public static void SaveLanguage(string code)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var json = File.Exists(FilePath) ? File.ReadAllText(FilePath) : "{}";

            var documentOptions = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            if (JsonNode.Parse(json, documentOptions: documentOptions) is not JsonObject root) return;

            root["Language"] = code;
            File.WriteAllText(FilePath, root.ToJsonString(Options));
        }
        catch
        {
            // Not critical: the language still changes for this session
        }
    }
}
