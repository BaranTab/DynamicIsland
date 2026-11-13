using System.Windows;
using DynamicIsland.Localization;
using DynamicIsland.Services;

namespace DynamicIsland;

public partial class App : Application
{
    private Mutex? _mutex;

    /// <summary>Settings loaded once at startup (see Services/SettingsService.cs).</summary>
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only one instance of the island may run at a time
        _mutex = new Mutex(true, "DynamicIsland_SingleInstance", out bool isNew);
        if (!isNew)
        {
            Shutdown();
            return;
        }

        Settings = SettingsService.Load();
        Loc.Instance.SetLanguage(Settings.Language);

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
