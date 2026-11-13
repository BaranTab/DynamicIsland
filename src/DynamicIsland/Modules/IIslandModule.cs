namespace DynamicIsland.Modules;

/// <summary>
/// Every feature the island can show (clock, media, weather, mail…) implements this interface.
/// Among the active modules, the one with the highest priority is shown in the compact island.
/// </summary>
public interface IIslandModule : IDisposable
{
    string Name { get; }

    /// <summary>Higher value = shown first in the compact island.</summary>
    int Priority { get; }

    /// <summary>Does the module have something to show right now?</summary>
    bool IsActive { get; }

    /// <summary>Raised when data changes (may come from a background thread).</summary>
    event EventHandler? Changed;

    Task StartAsync();
}
