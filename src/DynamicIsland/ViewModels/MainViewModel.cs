using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DynamicIsland.Modules;
using DynamicIsland.Services;

namespace DynamicIsland.ViewModels;

/// <summary>Kapalı (compact) adanın ne gösterdiği.</summary>
public enum IslandMode { Idle, Music, Timer }

public sealed record DayCell(string Day, double Opacity, Visibility TodayVisibility, Visibility EventDotVisibility,
                             Brush Foreground, FontWeight Weight);

public sealed record EventItem(string When, string Title);

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly System.Globalization.CultureInfo Tr = Format.Tr;
    private readonly List<IIslandModule> _modules;
    private byte[]? _lastThumbnail;
    private (DateTime Day, int Version) _calendarStamp;

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
    }

    public async Task StartAsync()
    {
        // Modüller birbirini beklemesin (posta sunucusu yavaşsa saat donmasın)
        var tasks = _modules.Select(async module =>
        {
            try { await module.StartAsync(); }
            catch { /* Bir modül çökerse diğerleri çalışmaya devam etsin */ }
        }).ToList();
        Refresh();
        await Task.WhenAll(tasks);
        Refresh();
    }

    // =========================== Üst bilgi satırı ===========================

    public string TimeText => Clock.Now.ToString("HH:mm", Tr);
    public string HeaderLeft => $"{Clock.Now.ToString("ddd dd MMM", Tr).ToUpper(Tr)}  /  {WeatherTemp}";

    // ============================== Kapalı ada ==============================

    public IslandMode Mode => Focus.IsRunning ? IslandMode.Timer
                            : Media.IsActive ? IslandMode.Music
                            : IslandMode.Idle;

    public Visibility IdleVisibility => Show(Mode == IslandMode.Idle);
    public Visibility MusicCompactVisibility => Show(Mode == IslandMode.Music);
    public Visibility TimerCompactVisibility => Show(Mode == IslandMode.Timer);

    // ================================ Medya =================================

    public string MediaTitle => Media.Title;
    public string MediaArtist => Media.Artist;
    public string MediaSourceLabel => string.IsNullOrEmpty(Media.SourceApp)
        ? "ŞİMDİ ÇALIYOR"
        : $"ŞİMDİ ÇALIYOR  ·  {Media.SourceApp.ToUpper(Tr)}";
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

    // ================================= Odak =================================

    public string FocusRemainingText
    {
        get
        {
            int secs = (int)Math.Ceiling(Focus.Remaining.TotalSeconds);
            return $"{secs / 60:00}:{secs % 60:00}";
        }
    }

    public string FocusStatusText =>
        Focus.IsRunning ? "Çalışıyor" :
        Focus.IsFinished ? "Tamamlandı" :
        Focus.IsPaused ? "Duraklatıldı" : "Hazır";

    public string FocusEndText => Focus.IsRunning && Focus.EndsAt is DateTime end
        ? $"Bitiş {end:HH:mm}"
        : $"Şimdi başlarsan bitiş {(DateTime.Now + (Focus.IsFinished ? Focus.Duration : Focus.Remaining)):HH:mm}";

    public string FocusButtonText => Focus.IsRunning ? "Duraklat" : Focus.IsPaused ? "Devam" : "Başlat";

    // ============================= Hava durumu ==============================

    public string WeatherIcon => Weather.Icon;
    public string WeatherTemp => Weather.Temperature is double t ? $"{t:0}°" : "--°";
    public string WeatherDescription => Weather.IsActive ? Weather.Description : "Yükleniyor…";
    public string WeatherShort => Weather.IsActive ? $"{Weather.LocationName} · {Weather.Description}" : "Yükleniyor…";
    public string WeatherLocationLabel => Weather.LocationName.ToUpper(Tr);
    public string WeatherMinMax => Weather.MaxTemperature is double max && Weather.MinTemperature is double min
        ? $"En yüksek {max:0}°  ·  En düşük {min:0}°"
        : "";
    public IReadOnlyList<HourlyForecast> HourlyForecasts => Weather.Hourly;

    /// <summary>Hava durumuna göre camın rengi.</summary>
    public Color WeatherAccent => Weather.WeatherCode switch
    {
        0 => Color.FromRgb(0xE0, 0x8A, 0x3C),                                      // güneşli
        1 or 2 or 3 => Color.FromRgb(0x6F, 0x86, 0xA8),                            // parçalı bulutlu
        45 or 48 => Color.FromRgb(0x84, 0x86, 0x8C),                               // sis
        (>= 51 and <= 67) or (>= 80 and <= 82) => Color.FromRgb(0x3E, 0x6F, 0xB8), // yağmur
        (>= 71 and <= 77) or 85 or 86 => Color.FromRgb(0x9F, 0xB7, 0xD6),         // kar
        >= 95 => Color.FromRgb(0x5B, 0x4A, 0x9A),                                  // fırtına
        _ => Color.FromRgb(0x6F, 0x7A, 0x8C)
    };

    // ================================= Posta ================================

    public Visibility MailReadyVisibility => Show(Mail.IsConfigured);
    public Visibility MailSetupVisibility => Show(!Mail.IsConfigured);
    public Visibility MailEmptyVisibility => Show(Mail.IsConfigured && Mail.Messages.Count == 0);
    public string MailUnreadText => Mail.UnreadCount?.ToString() ?? "–";
    public string MailUnreadCaption => !Mail.IsConfigured ? "Bağlı değil"
                                     : Mail.UnreadCount is null ? "Yükleniyor…"
                                     : "okunmamış";
    public string MailAccount => Mail.IsConfigured ? Mail.Account : "Hesap eklenmedi";
    public string MailStatus => Mail.Status;
    public IReadOnlyList<MailItem> MailMessages => Mail.Messages;

    // ================================ Takvim ================================

    public string CalendarMonthTitle => Clock.Now.ToString("MMMM yyyy", Tr).ToUpper(Tr);
    public string CalendarTodayTitle => Clock.Now.ToString("d MMMM, dddd", Tr);
    public IReadOnlyList<DayCell> CalendarDays { get; private set; } = Array.Empty<DayCell>();
    public IReadOnlyList<EventItem> UpcomingEvents { get; private set; } = Array.Empty<EventItem>();
    public Visibility CalendarSetupVisibility => Show(!Calendar.IsConfigured);
    public Visibility NoEventsVisibility => Show(Calendar.IsConfigured && UpcomingEvents.Count == 0);
    public string CalendarStatus => Calendar.Status;

    // ============================== Claude Code =============================

    public IReadOnlyList<ProjectItem> ClaudeProjects => Claude.Projects;
    public Visibility ClaudeEmptyVisibility => Show(Claude.Projects.Count == 0);
    public string ClaudePromptFolder => Claude.Projects.FirstOrDefault()?.Name ?? "proje";

    // ================================ VS Code ===============================

    public IReadOnlyList<RecentItem> VsCodeRecents => VsCode.Recents;
    public Visibility VsCodeEmptyVisibility => Show(VsCode.Recents.Count == 0);

    // ========================================================================

    private void Refresh()
    {
        if (!ReferenceEquals(_lastThumbnail, Media.Thumbnail))
        {
            _lastThumbnail = Media.Thumbnail;
            AlbumArt = ToImage(_lastThumbnail);
            AlbumAccent = ColorExtractor.FromImageBytes(_lastThumbnail);
        }

        var stamp = (DateTime.Today, Calendar.Version);
        if (stamp != _calendarStamp)
        {
            _calendarStamp = stamp;
            RebuildCalendar();
        }

        // Boş isim = tüm binding'leri yenile
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    /// <summary>Pazartesiden başlayan 6 haftalık ay görünümü.</summary>
    private void RebuildCalendar()
    {
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
            var day = e.Start.Date == today ? "Bugün"
                    : e.Start.Date == today.AddDays(1) ? "Yarın"
                    : e.Start.ToString("ddd d MMM", Tr);
            var time = e.AllDay ? "Tüm gün" : e.Start.ToString("HH:mm");
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
