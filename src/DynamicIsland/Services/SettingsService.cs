using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicIsland.Services;

public sealed class AppSettings
{
    public WeatherSettings Weather { get; set; } = new();
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

public sealed class MailSettings
{
    /// <summary>Gmail için imap.gmail.com, Outlook için outlook.office365.com</summary>
    public string ImapHost { get; set; } = "imap.gmail.com";
    public int ImapPort { get; set; } = 993;
    public string Username { get; set; } = "";
    /// <summary>Gmail'de normal şifre değil, "Uygulama şifresi" gerekir.</summary>
    public string AppPassword { get; set; } = "";
    public string WebUrl { get; set; } = "https://mail.google.com";

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(AppPassword);
}

public sealed class CalendarSettings
{
    /// <summary>Google Takvim → Ayarlar → Takvimi entegre et → "iCal biçimindeki gizli adres"</summary>
    public string IcsUrl { get; set; } = "";
    public string WebUrl { get; set; } = "https://calendar.google.com";
}

public sealed class ClaudeCodeSettings
{
    /// <summary>Otomatik bulunanlara ek olarak gösterilecek proje klasörleri.</summary>
    public List<string> Projects { get; set; } = new();
}

public sealed class VsCodeSettings
{
    /// <summary>Boş bırakılırsa Code.exe otomatik aranır.</summary>
    public string? CodePath { get; set; }
    public List<string> ExtraFolders { get; set; } = new();
}

/// <summary>
/// Ayarlar %AppData%\DynamicIsland\settings.json dosyasında tutulur.
/// Dosya yoksa varsayılan değerlerle oluşturulur.
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
        AllowTrailingCommas = true
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
            return new AppSettings();
        }
    }
}
