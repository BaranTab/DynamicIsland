using System.Windows;
using System.Windows.Controls;
using DynamicIsland.Services;

namespace DynamicIsland.Views.Pages;

public partial class CalendarPage : UserControl
{
    public CalendarPage() => InitializeComponent();

    private void OpenCalendar_Click(object sender, RoutedEventArgs e)
    {
        if (this.Vm() is { } vm) Launcher.OpenUrl(vm.Calendar.WebUrl);
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) => Launcher.OpenSettingsFile();
}
