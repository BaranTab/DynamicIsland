using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynamicIsland.Localization;
using DynamicIsland.Modules;
using DynamicIsland.Services;

namespace DynamicIsland.ViewModels;

/// <summary>What the collapsed (compact) island shows.</summary>
public enum IslandMode { Idle, Music, Timer }

public sealed record DayCell(string Day, double Opacity, Visibility TodayVisibility, Visibility EventDotVisibility,
                             Brush Foreground, FontWeight Weight);

public sealed record EventItem(string When, string Title);

/// <summary>
/// Connects the modules to the UI. Every module raises Changed; we then raise
/// PropertyChanged with an empty name, which refreshes all bindings at once.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly List<IIslandModule> _modules;
    private byte[]? _lastThumbnail;
    private (DateTime Day, int Version, string Language) _calendarStamp;

    public AppSettings Settings { get; }

    public ClockModule Clock { get; } = new();
    public MediaModule Media { get; } = new();
    public WeatherModule Weather { get; }
    public FocusModule Focus { get; } = new();
    public MailModule Mail { get; }
    public CalendarModule Calendar { get; }
    public ClaudeCodeModule Claude { get; }
    public VsCodeModule VsCode { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private static CultureInfo Culture => Loc.Instance.Culture;

    public MainViewModel(AppSettings settings)
    {
        Settings = settings;
        Weather = new WeatherModule(settings.Weather.Latitude, settings.Weather.Longitude, settings.Weather.LocationName);
        Mail = new MailModule(settings.Mail);
        Calendar = new CalendarModule(settings.Calendar);
        Claude = new ClaudeCodeModule(settings.ClaudeCode);
        VsCode = new VsCodeModule(settings.VsCode);

        _modules = new List<IIslandModule> { Clock, Media, Weather, Focus, Mail, Calendar, Claude, VsCode };
        foreach (var module in _modules)
            module.Changed += (_, _) => Application.Current?.Dispatcher.InvokeAsync(Refresh);

        // Texts built ahead of time ("2 h ago" etc.) are rebuilt in the new language
        Loc.Instance.LanguageChanged += (_, _) =>
        {
            _ = Claude.RefreshAsync();
            _ = VsCode.RefreshAsync();
            Refresh();
        };
    }

    public async Task StartAsync()
    {
        // Modules don't wait for each other (a slow mail server must not freeze the clock)
        var tasks = _modules.Select(async module =>
        {
            try { await module.StartAsync(); }
            catch { /* If one module fails, the others keep working */ }
        }).ToList();

        Refresh();
        await Task.WhenAll(tasks);
        Refresh();
    }

    // =============================== Header ================================

    public string TimeText => Clock.Now.ToString("HH:mm", Culture);
    public string HeaderLeft => $"{Clock.Now.ToString("ddd dd MMM", Culture).ToUpper(Culture)}  /  {WeatherTemp}";

    // ============================ Compact island ===========================

    public IslandMode Mode => Focus.IsRunning ? IslandMode.Timer
                            : Media.IsActive ? IslandMode.Music
                            : IslandMode.Idle;

    public Visibility IdleVisibility => Show(Mode == IslandMode.Idle);
    public Visibility MusicCompactVisibility => Show(Mode == IslandMode.Music);
    public Visibility TimerCompactVisibility => Show(Mode == IslandMode.Timer);

    // ================================ Media ================================

    public string MediaTitle => Media.Title;
    public string MediaArtist => Media.Artist;
    public string MediaSourceLabel => string.IsNullOrEmpty(Media.SourceApp)
        ? Loc.T("NowPlaying")
        : $"{Loc.T("NowPlaying")}  ·  {Media.SourceApp.ToUpper(Culture)}";
    public string PlayPauseGlyph => Media.IsPlaying ? "" : ""; // Pause / Play
    public Visibility MediaVisibility => Show(Media.IsActive);
    public Visibility NoMediaVisibility => Show(!Media.IsActive);
    public ImageSource? AlbumArt { get; private set; }
    public Color? AlbumAccent { get; private set; }

    public double ProgressValue => Media.Duration.TotalSeconds > 0
        ? Math.Clamp(Media.CurrentPosition.TotalSeconds / Media.Duration.TotalSeconds, 0, 1)
        : 0;
    public string ElapsedText => FormatTime(Media.CurrentPosition);
    public string TotalText => FormatTime(Media.Duration);
    public Visibility ProgressVisibility => Show(Media.Duration.TotalSeconds > 0);

    // ================================ Focus ================================

    public string FocusRemainingText
    {
        get
        {
            int secs = (int)Math.Ceiling(Focus.Remaining.TotalSeconds);
            return $"{secs / 60:00}:{secs % 60:00}";
        }
    }

    public string FocusStatusText => Loc.T(
        Focus.IsRunning ? "FocusRunning" :
        Focus.IsFinished ? "FocusFinished" :
        Focus.IsPaused ? "FocusPaused" : "FocusReady");

    public string FocusEndText => Focus.IsRunning && Focus.EndsAt is DateTime end
        ? Loc.T("FocusEndsAt", end.ToString("HH:mm"))
        : Loc.T("FocusEndsIfStarted", (DateTime.Now + (Focus.IsFinished ? Focus.Duration : Focus.Remaining)).ToString("HH:mm"));

    public string FocusButtonText => Loc.T(Focus.IsRunning ? "FocusPause" : Focus.IsPaused ? "FocusResume" : "FocusStart");

    // =============================== Weather ===============================

    public string WeatherIcon => Weather.Icon;
    public string WeatherTemp => Weather.Temperature is double t ? $"{t:0}°" : "--°";
    public string WeatherDescription => Weather.IsActive ? Loc.T(Weather.DescriptionKey) : Loc.T("Loading");
    public string WeatherShort => Weather.IsActive ? $"{Weather.LocationName} · {Loc.T(Weather.DescriptionKey)}" : Loc.T("Loading");
    public string WeatherLocationLabel => Weather.LocationName.ToUpper(Culture);
    public string WeatherMinMax => Weather.MaxTemperature is double max && Weather.MinTemperature is double min
        ? Loc.T("WeatherHighLow", max.ToString("0"), min.ToString("0"))
        : "";
    public IReadOnlyList<HourlyForecast> HourlyForecasts => Weather.Hourly;

    /// <summary>The glass colour on the weather page, based on the conditions.</summary>
    public Color WeatherAccent => Weather.WeatherCode switch
    {
        0 => Color.FromRgb(0xE0, 0x8A, 0x3C),                                      // sunny
        1 or 2 or 3 => Color.FromRgb(0x6F, 0x86, 0xA8),                            // partly cloudy
        45 or 48 => Color.FromRgb(0x84, 0x86, 0x8C),                               // fog
        (>= 51 and <= 67) or (>= 80 and <= 82) => Color.FromRgb(0x3E, 0x6F, 0xB8), // rain
        (>= 71 and <= 77) or 85 or 86 => Color.FromRgb(0x9F, 0xB7, 0xD6),         // snow
        >= 95 => Color.FromRgb(0x5B, 0x4A, 0x9A),                                  // storm
        _ => Color.FromRgb(0x6F, 0x7A, 0x8C)
    };

    // ================================= Mail ================================

    public Visibility MailReadyVisibility => Show(Mail.IsConfigured);
    public Visibility MailSetupVisibility => Show(!Mail.IsConfigured);
    public Visibility MailEmptyVisibility => Show(Mail.IsConfigured && Mail.Messages.Count == 0);
    public string MailUnreadText => Mail.UnreadCount?.ToString() ?? "–";
    public string MailUnreadCaption => Loc.T(!Mail.IsConfigured ? "MailNotConnected"
                                           : Mail.UnreadCount is null ? "Loading"
                                           : "MailUnread");
    public string MailAccount => Mail.IsConfigured ? Mail.Account : Loc.T("MailNoAccount");
    public string MailStatus => string.IsNullOrEmpty(Mail.StatusKey) ? "" : Loc.T(Mail.StatusKey, Mail.StatusArgument);
    public IReadOnlyList<MailItem> MailMessages => Mail.Messages;

    // =============================== Calendar ==============================

    public string CalendarMonthTitle => Clock.Now.ToString("MMMM yyyy", Culture).ToUpper(Culture);
    public string CalendarTodayTitle => Clock.Now.ToString("d MMMM, dddd", Culture);
    public IReadOnlyList<string> WeekDayNames { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<DayCell> CalendarDays { get; private set; } = Array.Empty<DayCell>();
    public IReadOnlyList<EventItem> UpcomingEvents { get; private set; } = Array.Empty<EventItem>();
    public Visibility CalendarSetupVisibility => Show(!Calendar.IsConfigured);
    public Visibility NoEventsVisibility => Show(Calendar.IsConfigured && UpcomingEvents.Count == 0);
    public string CalendarStatus => string.IsNullOrEmpty(Calendar.StatusKey) ? "" : Loc.T(Calendar.StatusKey, Calendar.StatusArgument);

    // ============================= Claude Code =============================

    public IReadOnlyList<ProjectItem> ClaudeProjects => Claude.Projects;
    public Visibility ClaudeEmptyVisibility => Show(Claude.Projects.Count == 0);
    public string ClaudePromptFolder => Claude.Projects.FirstOrDefault()?.Name ?? Loc.T("ClaudeDefaultFolder");

    // =============================== VS Code ===============================

    public IReadOnlyList<RecentItem> VsCodeRecents => VsCode.Recents;
    public Visibility VsCodeEmptyVisibility => Show(VsCode.Recents.Count == 0);

    // =======================================================================

    private void Refresh()
    {
        if (!ReferenceEquals(_lastThumbnail, Media.Thumbnail))
        {
            _lastThumbnail = Media.Thumbnail;
            AlbumArt = ToImage(_lastThumbnail);
            AlbumAccent = ColorExtractor.FromImageBytes(_lastThumbnail);
        }

        var stamp = (DateTime.Today, Calendar.Version, Loc.Instance.Language);
        if (stamp != _calendarStamp)
        {
            _calendarStamp = stamp;
            RebuildCalendar();
        }

        // An empty property name refreshes every binding
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>A six-week month view starting on Monday.</summary>
    private void RebuildCalendar()
    {
        // Monday-first short day names in the current language (Mo, Tu… / Pt, Sa…)
        var names = Culture.DateTimeFormat.ShortestDayNames;
        WeekDayNames = Enumerable.Range(1, 7).Select(i => names[i % 7]).ToList();

        var today = DateTime.Today;
        var first = new DateTime(today.Year, today.Month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-offset);
        var eventDays = Calendar.Events.Select(e => e.Start.Date).ToHashSet();

        var cells = new List<DayCell>(42);
        for (int i = 0; i < 42; i++)
        {
            var day = start.AddDays(i);
            bool isToday = day == today;
            cells.Add(new DayCell(
                day.Day.ToString(),
                day.Month == today.Month ? 1 : 0.3,
                Show(isToday),
                Show(eventDays.Contains(day)),
                isToday ? Brushes.Black : Brushes.White,
                isToday ? FontWeights.Bold : FontWeights.Normal));
        }
        CalendarDays = cells;

        UpcomingEvents = Calendar.Events.Take(3).Select(e =>
        {
            var day = e.Start.Date == today ? Loc.T("CalendarToday")
                    : e.Start.Date == today.AddDays(1) ? Loc.T("CalendarTomorrow")
                    : e.Start.ToString("ddd d MMM", Culture);
            var time = e.AllDay ? Loc.T("CalendarAllDay") : e.Start.ToString("HH:mm");
            return new EventItem($"{day}  ·  {time}", e.Title);
        }).ToList();
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static string FormatTime(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    private static ImageSource? ToImage(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        foreach (var module in _modules) module.Dispose();
    }
}
