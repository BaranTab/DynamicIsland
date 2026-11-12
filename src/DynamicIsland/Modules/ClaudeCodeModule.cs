using System.IO;
using System.Text.Json;
using DynamicIsland.Services;

namespace DynamicIsland.Modules;

public sealed record ProjectItem(string Name, string Path, string Detail);

/// <summary>
/// Claude Code ile en son çalışılan projeleri bulur.
/// Claude Code oturumları ~/.claude/projects/&lt;proje&gt;/*.jsonl dosyalarında tutulur;
/// bu dosyalardaki "cwd" alanı projenin gerçek klasörünü verir.
/// </summary>
public sealed class ClaudeCodeModule : IIslandModule
{
    private readonly ClaudeCodeSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(5));

    public ClaudeCodeModule(ClaudeCodeSettings settings) => _settings = settings;

    public string Name => "Claude Code";
    public int Priority => 0;
    public bool IsActive => false;

    public IReadOnlyList<ProjectItem> Projects { get; private set; } = Array.Empty<ProjectItem>();

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try { Projects = await Task.Run(Discover); }
        catch { /* Okunamadı */ }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<ProjectItem> Discover()
    {
        var found = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
        if (Directory.Exists(root))
        {
            foreach (var dir in Directory.EnumerateDirectories(root))
            {
                var latest = new DirectoryInfo(dir).EnumerateFiles("*.jsonl")
                    .OrderByDescending(f => f.LastWriteTime)
                    .FirstOrDefault();
                if (latest == null) continue;

                var cwd = ReadWorkingDirectory(latest.FullName);
                if (cwd == null || !Directory.Exists(cwd)) continue;

                if (!found.TryGetValue(cwd, out var time) || latest.LastWriteTime > time)
                    found[cwd] = latest.LastWriteTime;
            }
        }

        foreach (var path in _settings.Projects)
        {
            if (Directory.Exists(path) && !found.ContainsKey(path))
                found[path] = DateTime.MinValue;
        }

        return found
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => new ProjectItem(
                Format.FolderName(kv.Key),
                kv.Key,
                kv.Value == DateTime.MinValue
                    ? Format.ShortPath(kv.Key)
                    : $"{Format.Relative(kv.Value)}  ·  {Format.ShortPath(kv.Key)}"))
            .ToList();
    }

    private static string? ReadWorkingDirectory(string file)
    {
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);

            for (int i = 0; i < 30 && reader.ReadLine() is string line; i++)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("cwd", out var cwd) && cwd.ValueKind == JsonValueKind.String)
                        return cwd.GetString();
                }
                catch (JsonException) { /* Bu satırı atla */ }
            }
        }
        catch { /* Dosya kilitli olabilir */ }

        return null;
    }

    public void Dispose() => _timer.Dispose();
}
