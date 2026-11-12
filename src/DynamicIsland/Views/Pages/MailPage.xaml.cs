using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicIsland.Services;

namespace DynamicIsland.Views.Pages;

public partial class MailPage : UserControl
{
    public MailPage() => InitializeComponent();

    private void OpenInbox_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) Launcher.OpenUrl(vm.Mail.WebUrl);
    }

    private void Row_Click(object sender, MouseButtonEventArgs e) => OpenInbox_Click(sender, e);

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) await vm.Mail.RefreshAsync();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => Launcher.OpenSettingsFile();
}
