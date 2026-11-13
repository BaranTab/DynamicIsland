using System.IO;
using System.Text.Json;

namespace DynamicIsland.Services;

public sealed class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    public bool Done { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
}

/// <summary>Tasks are stored in %AppData%\DynamicIsland\todos.json.</summary>
public static class TodoStore
{
    private static string FilePath => Path.Combine(SettingsService.Folder, "todos.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<TodoItem> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<TodoItem>>(File.ReadAllText(FilePath)) ?? new List<TodoItem>();
        }
        catch { /* Corrupt file: start empty */ }
        return new List<TodoItem>();
    }

    public static void Save(IEnumerable<TodoItem> items)
    {
        try
        {
            Directory.CreateDirectory(SettingsService.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(items, Options));
        }
        catch { /* Could not save */ }
    }
}
