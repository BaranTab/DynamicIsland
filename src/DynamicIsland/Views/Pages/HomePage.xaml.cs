using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DynamicIsland.Views.Pages;

public partial class HomePage : UserControl
{
    public HomePage() => InitializeComponent();

    private void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (int.TryParse(sender.TagText(), out int page))
            this.Island()?.GoToPage(page);
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) await vm.Media.TogglePlayPauseAsync();
    }
}
