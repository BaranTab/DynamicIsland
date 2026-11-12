using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicIsland.Services;
using Microsoft.Win32;

namespace DynamicIsland.Views.Pages;

public partial class VsCodePage : UserControl
{
    public VsCodePage() => InitializeComponent();

    private string? CodePath => this.Vm()?.VsCode.CodePath;

    private void Recent_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender.TagText() is { } path) Launcher.OpenInVsCode(path, CodePath);
    }

    private void NewWindow_Click(object sender, RoutedEventArgs e) =>
        Launcher.OpenInVsCode(null, CodePath, newWindow: true);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "VS Code'da açılacak klasör" };
        if (dialog.ShowDialog() == true)
            Launcher.OpenInVsCode(dialog.FolderName, CodePath);
    }
}
