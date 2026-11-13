using System.Windows.Data;
using System.Windows.Markup;

namespace DynamicIsland.Localization;

/// <summary>
/// XAML shortcut for translated text: <c>Text="{loc:T NowPlaying}"</c>
/// It creates a live binding, so the text updates as soon as the language changes.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }

    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
