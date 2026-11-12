using System.IO;
using System.Text.Json;

namespace DynamicIsland.Services;

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public DateTime Updated { get; set; } = DateTime.Now;

    public string Title
    {
        get
        {
            var first = Text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
            return first ?? "Boş not";
        }
    }
}

/// <summary>Notlar %AppData%\DynamicIsland\notes.json dosyasında tutulur.</summary>
public static class NotesStore
{
    private static string FilePath => Path.Combine(SettingsService.Folder, "notes.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<Note> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<Note>>(File.ReadAllText(FilePath)) ?? new List<Note>();
        }
        catch { /* Bozuk dosya: boş başla */ }
        return new List<Note>();
    }

    public static void Save(IEnumerable<Note> notes)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Folder);
            var toSave = notes.Where(n => !string.IsNullOrWhiteSpace(n.Text)).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(toSave, Options));
        }
        catch { /* Kaydedilemedi */ }
    }
}
