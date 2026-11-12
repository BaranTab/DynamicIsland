namespace DynamicIsland.Modules;

/// <summary>
/// Pomodoro tarzı odak zamanlayıcısı.
/// </summary>
public sealed class FocusModule : IIslandModule
{
    private readonly System.Timers.Timer _timer = new(250);

    public string Name => "Odak";
    public int Priority => 20; // Çalışırken her şeyin önünde
    public bool IsActive => IsRunning;

    public TimeSpan Duration { get; private set; } = TimeSpan.FromMinutes(25);
    public TimeSpan Remaining { get; private set; } = TimeSpan.FromMinutes(25);
    public bool IsRunning { get; private set; }
    public DateTime? EndsAt { get; private set; }

    public bool IsPaused => !IsRunning && Remaining > TimeSpan.Zero && Remaining < Duration;
    public bool IsFinished => !IsRunning && Remaining <= TimeSpan.Zero;

    public event EventHandler? Changed;
    public event EventHandler? Completed;

    public Task StartAsync()
    {
        _timer.Elapsed += (_, _) => Tick();
        _timer.Start();
        return Task.CompletedTask;
    }

    public void SetDuration(double minutes)
    {
        if (IsRunning) return;
        Duration = Remaining = TimeSpan.FromMinutes(Math.Clamp(Math.Round(minutes), 1, 60));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ToggleStartPause()
    {
        if (IsRunning) Pause();
        else Start();
    }

    public void Start()
    {
        if (Remaining <= TimeSpan.Zero) Remaining = Duration;
        EndsAt = DateTime.Now + Remaining;
        IsRunning = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Pause()
    {
        if (!IsRunning || EndsAt is null) return;
        Remaining = EndsAt.Value - DateTime.Now;
        IsRunning = false;
        EndsAt = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        IsRunning = false;
        EndsAt = null;
        Remaining = Duration;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Tick()
    {
        if (!IsRunning || EndsAt is null) return;

        Remaining = EndsAt.Value - DateTime.Now;
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            IsRunning = false;
            EndsAt = null;
            Changed?.Invoke(this, EventArgs.Empty);
            Completed?.Invoke(this, EventArgs.Empty);
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _timer.Dispose();
}
