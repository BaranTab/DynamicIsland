using Windows.Media.Control;
using Windows.Storage.Streams;

namespace DynamicIsland.Modules;

/// <summary>
/// Windows'un Global System Media Transport Controls API'si ile çalan medyayı okur.
/// Spotify, tarayıcıdaki YouTube, Apple Music vb. (medya tuşlarını destekleyen her uygulama) çalışır.
/// </summary>
public sealed class MediaModule : IIslandModule
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private DateTimeOffset _timelineUpdatedAt;

    public string Name => "Medya";
    public int Priority => 10;
    public bool IsActive => _session != null && !string.IsNullOrWhiteSpace(Title);

    public string Title { get; private set; } = "";
    public string Artist { get; private set; } = "";
    public string SourceApp { get; private set; } = "";
    public bool IsPlaying { get; private set; }
    public byte[]? Thumbnail { get; private set; }

    public TimeSpan Duration { get; private set; }
    private TimeSpan _position;

    /// <summary>Uygulamalar konumu seyrek bildirir; çalarken aradaki süreyi biz ekliyoruz.</summary>
    public TimeSpan CurrentPosition
    {
        get
        {
            var p = _position;
            if (IsPlaying && _timelineUpdatedAt.Year > 2000)
                p += DateTimeOffset.Now - _timelineUpdatedAt;
            if (p < TimeSpan.Zero) p = TimeSpan.Zero;
            if (Duration > TimeSpan.Zero && p > Duration) p = Duration;
            return p;
        }
    }

    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += (_, _) => _ = AttachAsync(_manager.GetCurrentSession());
        await AttachAsync(_manager.GetCurrentSession());
    }

    private async Task AttachAsync(GlobalSystemMediaTransportControlsSession? session)
    {
        Detach();
        _session = session;

        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;
        }

        await RefreshAsync(includeThumbnail: true);
    }

    private void Detach()
    {
        if (_session == null) return;
        _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        _session.TimelinePropertiesChanged -= OnTimelineChanged;
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs e)
        => _ = RefreshAsync(includeThumbnail: true);

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs e)
        => _ = RefreshAsync(includeThumbnail: false);

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession s, TimelinePropertiesChangedEventArgs e)
        => _ = RefreshAsync(includeThumbnail: false);

    private async Task RefreshAsync(bool includeThumbnail)
    {
        try
        {
            if (_session == null)
            {
                Title = Artist = SourceApp = "";
                IsPlaying = false;
                Thumbnail = null;
                Duration = _position = TimeSpan.Zero;
            }
            else
            {
                var props = await _session.TryGetMediaPropertiesAsync();
                Title = props?.Title ?? "";
                Artist = props?.Artist ?? "";
                SourceApp = CleanAppName(_session.SourceAppUserModelId);
                IsPlaying = _session.GetPlaybackInfo()?.PlaybackStatus
                            == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                var timeline = _session.GetTimelineProperties();
                if (timeline != null)
                {
                    Duration = timeline.EndTime - timeline.StartTime;
                    _position = timeline.Position;
                    _timelineUpdatedAt = timeline.LastUpdatedTime;
                }

                if (includeThumbnail)
                    Thumbnail = props?.Thumbnail != null ? await ReadThumbnailAsync(props.Thumbnail) : null;
            }
        }
        catch
        {
            // Okuma sırasında oturum kapanabilir; sessizce geç.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static async Task<byte[]> ReadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        using var stream = await reference.OpenReadAsync();
        using var reader = new DataReader(stream);
        await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[stream.Size];
        reader.ReadBytes(bytes);
        return bytes;
    }

    /// <summary>"SpotifyAB.SpotifyMusic_xxx!Spotify" → "Spotify", "msedge.exe" → "Edge"</summary>
    private static string CleanAppName(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";

        var name = id;
        int bang = name.LastIndexOf('!');
        if (bang >= 0) name = name[(bang + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];

        return name.ToLowerInvariant() switch
        {
            "msedge" => "Edge",
            "chrome" => "Chrome",
            "firefox" => "Firefox",
            "opera" => "Opera",
            _ => name
        };
    }

    public async Task TogglePlayPauseAsync()
    {
        if (_session != null) await _session.TryTogglePlayPauseAsync();
    }

    public async Task NextAsync()
    {
        if (_session != null) await _session.TrySkipNextAsync();
    }

    public async Task PreviousAsync()
    {
        if (_session != null) await _session.TrySkipPreviousAsync();
    }

    public void Dispose() => Detach();
}
