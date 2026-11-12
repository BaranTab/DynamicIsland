namespace DynamicIsland.Modules;

/// <summary>
/// Adada gösterilebilen her özellik (saat, medya, hava durumu, pil...) bu arayüzü uygular.
/// Aktif modüller arasında en yüksek önceliğe sahip olan, compact görünümde gösterilir.
/// </summary>
public interface IIslandModule : IDisposable
{
    string Name { get; }

    /// <summary>Yüksek değer = compact görünümde öncelik.</summary>
    int Priority { get; }

    /// <summary>Modülün şu an gösterecek bir şeyi var mı?</summary>
    bool IsActive { get; }

    /// <summary>Veri değiştiğinde tetiklenir (arka plan thread'inden gelebilir).</summary>
    event EventHandler? Changed;

    Task StartAsync();
}
