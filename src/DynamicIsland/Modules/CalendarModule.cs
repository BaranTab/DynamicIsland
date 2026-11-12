using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using DynamicIsland.Services;

namespace DynamicIsland.Modules;

public sealed record CalendarEvent(DateTime Start, bool AllDay, string Title);

/// <summary>
/// iCal (.ics) adresinden etkinlikleri okur. Google Takvim, Outlook ve Apple Takvim iCal adresi verir.
/// Not: Tekrarlayan etkinlikler (RRULE) şimdilik sadece ilk tarihleriyle görünür.
/// </summary>
public sealed class CalendarModule : IIslandModule
{
    private static readonly HttpClient Http = new();
    private readonly CalendarSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(15));

    public CalendarModule(CalendarSettings settings) => _settings = settings;

    public string Name => "Takvim";
    public int Priority => 1;
    public bool IsActive => false;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.IcsUrl);
    public string WebUrl => _settings.WebUrl;
    public IReadOnlyList<CalendarEvent> Events { get; private set; } = Array.Empty<CalendarEvent>();
    public string Status { get; private set; } = "";

    /// <summary>Her yenilemede artar; arayüz takvimi sadece değişince yeniden çizer.</summary>
    public int Version { get; private set; }

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.Start();
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsConfigured)
        {
            try
            {
                var url = Regex.Replace(_settings.IcsUrl.Trim(), "^webcal://", "https://", RegexOptions.IgnoreCase);
                var text = await Http.GetStringAsync(url);
                Events = Parse(text);
                Status = $"Güncellendi {DateTime.Now:HH:mm}";
            }
            catch (Exception ex)
            {
                Status = "Takvim alınamadı: " + ex.Message;
            }
        }

        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static List<CalendarEvent> Parse(string ics)
    {
        // Uzun satırlar bir sonraki satıra boşlukla devam eder ("line folding")
        var unfolded = Regex.Replace(ics, @"\r?\n[ \t]", "");
        var events = new List<CalendarEvent>();

        bool inEvent = false;
        string? summary = null, start = null;

        foreach (var raw in unfolded.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line == "BEGIN:VEVENT") { inEvent = true; summary = start = null; continue; }
            if (line == "END:VEVENT")
            {
                if (start != null && TryParseDate(start, out var date, out bool allDay))
                    events.Add(new CalendarEvent(date, allDay, summary ?? "(Başlıksız)"));
                inEvent = false;
                continue;
            }
            if (!inEvent) continue;

            int colon = line.IndexOf(':');
            if (colon < 0) continue;

            var property = line[..colon].Split(';')[0].ToUpperInvariant();
            var value = line[(colon + 1)..];

            if (property == "SUMMARY") summary = Unescape(value);
            else if (property == "DTSTART") start = value;
        }

        var today = DateTime.Today;
        return events
            .Where(e => e.Start >= today && e.Start < today.AddDays(60))
            .OrderBy(e => e.Start)
            .ToList();
    }

    private static bool TryParseDate(string value, out DateTime date, out bool allDay)
    {
        var inv = CultureInfo.InvariantCulture;
        allDay = false;

        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", inv, DateTimeStyles.None, out date))
        {
            allDay = true;
            return true;
        }

        if (value.EndsWith('Z') &&
            DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", inv,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            date = date.ToLocalTime();
            return true;
        }

        // TZID'li saatler yerel saat kabul edilir
        return DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", inv, DateTimeStyles.None, out date);
    }

    private static string Unescape(string value) => value
        .Replace("\\n", " ").Replace("\\N", " ")
        .Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    public void Dispose() => _timer.Dispose();
}
