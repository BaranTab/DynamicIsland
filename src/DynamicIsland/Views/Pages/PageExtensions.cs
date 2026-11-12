using System.Windows;
using DynamicIsland.ViewModels;

namespace DynamicIsland.Views.Pages;

internal static class PageExtensions
{
    public static MainViewModel? Vm(this FrameworkElement element) => element.DataContext as MainViewModel;

    public static IslandWindow? Island(this DependencyObject element) => Window.GetWindow(element) as IslandWindow;

    public static string? TagText(this object sender) => (sender as FrameworkElement)?.Tag as string;
}
