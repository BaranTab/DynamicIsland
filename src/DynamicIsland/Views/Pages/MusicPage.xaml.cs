using System.Windows;
using System.Windows.Controls;

namespace DynamicIsland.Views.Pages;

public partial class MusicPage : UserControl
{
    public MusicPage() => InitializeComponent();

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) await vm.Media.TogglePlayPauseAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) await vm.Media.NextAsync();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) await vm.Media.PreviousAsync();
    }
}
