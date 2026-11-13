using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynamicIsland.Services;

namespace DynamicIsland.Modules;

public sealed record LyricLine(TimeSpan Time, string Text);

public enum LyricsState { None, Searching, Found, NotFound }

/// <summary>
/// Fetches time-synced lyrics from LRCLIB (https://lrclib.net) — a free, open lyrics database that needs no API key.
///
/// Lookup order:
///   1. /api/get with title + artist + album + duration (exact match, ±2 s)
///   2. /api/search with title + artist (pick the closest duration that has synced lyrics)
///   3. For YouTube-style titles such as "Artist - Song (Official Video)": split and search again
///
/// Results are cached in memory for the session, so replaying a song doesn't hit the network.
/// </summary>
public sealed class LyricsModule : IIslandModule
{
    private static readonly HttpClient Http = CreateClient();

    private readonly LyricsSettings _settings;
    private readonly Dictionary<string, IReadOnlyList<LyricLine>> _cache = new();
    private string _currentKey = "";
    private CancellationTokenSource? _cts;

    public LyricsModule(LyricsSettings settings) => _settings = settings;

    public string Name => "Lyrics";
    public int Priority => 0;
    public bool IsActive => State == LyricsState.Found;

    public bool Enabled => _settings.Enabled;
    public bool ShowInCompactIsland => _settings.Enabled && _settings.ShowInCompactIsland;

    /// <summary>Positive values show lines later, negative values earlier.</summary>
    public TimeSpan Offset => TimeSpan.FromMilliseconds(_settings.OffsetMs);

    public IReadOnlyList<LyricLine> Lines { get; private set; } = Array.Empty<LyricLine>();
    public LyricsState State { get; private set; } = LyricsState.None;

    public event EventHandler? Changed;

    public Task StartAsync() => Task.CompletedTask;

    /// <summary>Called whenever the playing track changes. Does nothing if it's the same track.</summary>
    public async Task LoadAsync(string title, string artist, string album, TimeSpan duration)
    {
        if (!Enabled) return;

        var key = $"{artist}|{title}".ToLowerInvariant();
        if (key == _currentKey) return;
        _currentKey = key;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        if (string.IsNullOrWhiteSpace(title))
        {
            SetResult(Array.Empty<LyricLine>(), LyricsState.None);
            return;
        }

        if (_cache.TryGetValue(key, out var cached))
        {
            SetResult(cached, cached.Count > 0 ? LyricsState.Found : LyricsState.NotFound);
            return;
        }

        SetResult(Array.Empty<LyricLine>(), LyricsState.Searching);

        try
        {
            var lrc = await FetchAsync(title, artist, album, duration, token);
            if (token.IsCancellationRequested) return;

            var lines = lrc == null ? Array.Empty<LyricLine>() : Parse(lrc);
            _cache[key] = lines;
            SetResult(lines, lines.Count > 0 ? LyricsState.Found : LyricsState.NotFound);
        }
        catch (OperationCanceledException)
        {
            // A newer track started; its own lookup will report the result
        }
        catch
        {
            if (!token.IsCancellationRequested)
                SetResult(Array.Empty<LyricLine>(), LyricsState.NotFound);
        }
    }

    private void SetResult(IReadOnlyList<LyricLine> lines, LyricsState state)
    {
        Lines = lines;
        State = state;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Index of the line being sung at <paramref name="position"/>, or -1 before the first line.</summary>
    public int IndexAt(TimeSpan position)
    {
        var lines = Lines;
        int lo = 0, hi = lines.Count - 1, result = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (lines[mid].Time <= position) { result = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return result;
    }

    // ---------------------------------------------------------------- network

    private static async Task<string?> FetchAsync(string title, string artist, string album, TimeSpan duration, CancellationToken token)
    {
        var cleanTitle = CleanTitle(title);
        int seconds = (int)Math.Round(duration.TotalSeconds);

        // 1) Exact match
        if (!string.IsNullOrWhiteSpace(artist) && !string.IsNullOrWhiteSpace(album) && seconds > 0)
        {
            var url = "https://lrclib.net/api/get" +
                      $"?track_name={Uri.EscapeDataString(cleanTitle)}" +
                      $"&artist_name={Uri.EscapeDataString(artist)}" +
                      $"&album_name={Uri.EscapeDataString(album)}" +
                      $"&duration={seconds}";

            using var response = await Http.GetAsync(url, token);
            if (response.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (SyncedLyricsOf(doc.RootElement) is { } exact) return exact;
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        // 2) Search by title + artist
        var found = await SearchAsync(cleanTitle, artist, seconds, token);
        if (found != null) return found;

        // 3) YouTube style: "Artist - Song"
        int dash = cleanTitle.IndexOf(" - ", StringComparison.Ordinal);
        if (dash > 0)
        {
            var guessedArtist = cleanTitle[..dash].Trim();
            var guessedTitle = cleanTitle[(dash + 3)..].Trim();
            found = await SearchAsync(guessedTitle, guessedArtist, seconds, token);
        }

        return found;
    }

    private static async Task<string?> SearchAsync(string title, string artist, int seconds, CancellationToken token)
    {
        var url = $"https://lrclib.net/api/search?track_name={Uri.EscapeDataString(title)}";
        if (!string.IsNullOrWhiteSpace(artist)) url += $"&artist_name={Uri.EscapeDataString(artist)}";

        using var response = await Http.GetAsync(url, token);
        if (!response.IsSuccessStatusCode) return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

        // Prefer the result whose length is closest to the playing track
        string? best = null;
        double bestDiff = double.MaxValue;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (SyncedLyricsOf(item) is not { } synced) continue;

            double diff = seconds > 0 && item.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
                ? Math.Abs(d.GetDouble() - seconds)
                : 0;

            if (diff < bestDiff)
            {
                best = synced;
                bestDiff = diff;
            }
        }

        // A wildly different length usually means a different song or version
        return seconds > 0 && bestDiff > 10 ? null : best;
    }

    private static string? SyncedLyricsOf(JsonElement element) =>
        element.TryGetProperty("syncedLyrics", out var s) && s.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(s.GetString())
            ? s.GetString()
            : null;

    /// <summary>"Song (Official Video) [4K]" → "Song"</summary>
    private static string CleanTitle(string title)
    {
        var cleaned = Regex.Replace(title,
            @"\s*[\(\[][^\)\]]*(official|video|audio|lyrics?|visuali[sz]er|remaster(ed)?|hd|4k|mv)[^\)\]]*[\)\]]",
            "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }

    // ---------------------------------------------------------------- LRC parsing

    private static readonly Regex TimeTag = new(@"\[(\d{1,3}):(\d{1,2})(?:[.:](\d{1,3}))?\]", RegexOptions.Compiled);

    /// <summary>
    /// Parses LRC text: "[01:23.45] line". A line may carry several time tags.
    /// Empty lines (instrumental breaks) are kept as "♪".
    /// </summary>
    private static IReadOnlyList<LyricLine> Parse(string lrc)
    {
        var lines = new List<LyricLine>();

        foreach (var raw in lrc.Split('\n'))
        {
            var matches = TimeTag.Matches(raw);
            if (matches.Count == 0) continue;

            var text = TimeTag.Replace(raw, "").Trim();
            if (text.Length == 0) text = "♪";

            foreach (Match m in matches)
            {
                int minutes = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                int seconds = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                int millis = 0;
                if (m.Groups[3].Success)
                {
                    var fraction = m.Groups[3].Value;
                    millis = int.Parse(fraction, CultureInfo.InvariantCulture) * (fraction.Length switch { 1 => 100, 2 => 10, _ => 1 });
                }
                lines.Add(new LyricLine(new TimeSpan(0, 0, minutes, seconds, millis), text));
            }
        }

        return lines.OrderBy(l => l.Time).ToList();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        // LRCLIB asks clients to identify themselves
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland/1.0 (+https://github.com/BaranTab/DynamicIsland)");
        return client;
    }

    public void Dispose() => _cts?.Cancel();
}
