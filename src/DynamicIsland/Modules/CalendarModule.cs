using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using DynamicIsland.Localization;
using DynamicIsland.Services;

namespace DynamicIsland.Modules;

public sealed record CalendarEvent(DateTime Start, bool AllDay, string Title);

/// <summary>
/// Reads events from an iCal (.ics) URL. Google Calendar, Outlook and iCloud all provide one.
/// Note: recurring events (RRULE) currently only show their first occurrence.
/// </summary>
public sealed class CalendarModule : IIslandModule
{
    private static readonly HttpClient Http = new();
    private readonly CalendarSettings _settings;
    private readonly System.Timers.Timer _timer = new(TimeSpan.FromMinutes(15));

    public CalendarModule(CalendarSettings settings) => _settings = settings;

    public string Name => "Calendar";
    public int Priority => 1;
    public bool IsActive => false;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.IcsUrl);
    public string WebUrl => _settings.WebUrl;
    public IReadOnlyList<CalendarEvent> Events { get; private set; } = Array.Empty<CalendarEvent>();

    /// <summary>Translation key of the current status.</summary>
    public string StatusKey { get; private set; } = "";
    public string StatusArgument { get; private set; } = "";

    /// <summary>Incremented on every refresh, so the UI only rebuilds the month view when something changed.</summary>
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
                StatusKey = "CalendarStatusUpdated";
                StatusArgument = DateTime.Now.ToString("HH:mm");
            }
            catch (Exception ex)
            {
                StatusKey = "CalendarStatusFailed";
                StatusArgument = ex.Message;
            }
        }

        Version++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static List<CalendarEvent> Parse(string ics)
    {
        // Long lines continue on the next line starting with a space ("line folding")
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
                    events.Add(new CalendarEvent(date, allDay, summary ?? Loc.T("CalendarUntitled")));
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

        // All-day event: 20261113
        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd", inv, DateTimeStyles.None, out date))
        {
            allDay = true;
            return true;
        }

        // UTC time: 20261113T090000Z
        if (value.EndsWith('Z') &&
            DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss'Z'", inv,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            date = date.ToLocalTime();
            return true;
        }

        // Times with a TZID are treated as local time
        return DateTime.TryParseExact(value, "yyyyMMdd'T'HHmmss", inv, DateTimeStyles.None, out date);
    }

    private static string Unescape(string value) => value
        .Replace("\\n", " ").Replace("\\N", " ")
        .Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\");

    public void Dispose() => _timer.Dispose();
}
