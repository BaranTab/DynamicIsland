namespace DynamicIsland.Modules;

public sealed class ClockModule : IIslandModule
{
    private readonly System.Timers.Timer _timer = new(1000);

    public string Name => "Saat";
    public int Priority => 0;
    public bool IsActive => true;
    public DateTime Now { get; private set; } = DateTime.Now;

    public event EventHandler? Changed;

    public Task StartAsync()
    {
        _timer.Elapsed += (_, _) =>
        {
            Now = DateTime.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        };
        _timer.Start();
        return Task.CompletedTask;
    }

    public void Dispose() => _timer.Dispose();
}
