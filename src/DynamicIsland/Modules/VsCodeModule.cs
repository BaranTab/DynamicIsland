using System.IO;
using System.Text;
using System.Text.Json;
using DynamicIsland.Services;
using Microsoft.Data.Sqlite;

namespace DynamicIsland.Modules;

public sealed record RecentItem(string Name, string Path, string Detail, string Glyph);

/// <summary>
/// VS Code'un son açılan klasör/dosya listesini okur.
/// Liste %AppData%\Code\User\globalStorage\state.vscdb (SQLite) içinde
/// "history.recentlyOpenedPathsList" anahtarında JSON olarak tutulur.
/// </summary>
public sealed class VsCodeModule : IIslandModule
{
    private const string FolderGlyph = "";
    private const string FileGlyph = "";

    private readonly VsCodeSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(5));

    public VsCodeModule(VsCodeSettings settings) => _settings = settings;

    public string Name => "VS Code";
    public int Priority => 0;
    public bool IsActive => false;

    public string? CodePath => _settings.CodePath;
    public IReadOnlyList<RecentItem> Recents { get; private set; } = Array.Empty<RecentItem>();

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try { Recents = await Task.Run(Discover); }
        catch { /* Okunamadı */ }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<RecentItem> Discover()
    {
        var items = new List<RecentItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var db = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Code", "User", "globalStorage", "state.vscdb");

        if (File.Exists(db))
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = db,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM ItemTable WHERE key = 'history.recentlyOpenedPathsList'";

            string? json = command.ExecuteScalar() switch
            {
                string s => s,
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                _ => null
            };

            if (json != null)
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("entries", out var entries))
                {
                    foreach (var entry in entries.EnumerateArray())
                    {
                        if (items.Count >= 3) break;

                        string? uri = null;
                        bool isFolder = true;

                        if (entry.TryGetProperty("folderUri", out var folder)) uri = folder.GetString();
                        else if (entry.TryGetProperty("workspace", out var ws) && ws.TryGetProperty("configPath", out var cfg)) uri = cfg.GetString();
                        else if (entry.TryGetProperty("fileUri", out var file)) { uri = file.GetString(); isFolder = false; }

                        // Sadece yerel yollar (uzak/WSL bağlantıları atlanır)
                        if (uri == null || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || !parsed.IsFile) continue;

                        var path = parsed.LocalPath;
                        if (!seen.Add(path)) continue;

                        items.Add(new RecentItem(
                            Format.FolderName(path),
                            path,
                            Format.ShortPath(Path.GetDirectoryName(path) ?? path),
                            isFolder ? FolderGlyph : FileGlyph));
                    }
                }
            }
        }

        foreach (var extra in _settings.ExtraFolders)
        {
            if (items.Count >= 3) break;
            if (Directory.Exists(extra) && seen.Add(extra))
                items.Add(new RecentItem(Format.FolderName(extra), extra, Format.ShortPath(extra), FolderGlyph));
        }

        return items;
    }

    public void Dispose() => _timer.Dispose();
}
