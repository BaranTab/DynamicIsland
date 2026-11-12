using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DynamicIsland.Services;
using Microsoft.Win32;

namespace DynamicIsland.Views.Pages;

public partial class ClaudePage : UserControl
{
    public ClaudePage() => InitializeComponent();

    private void Project_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender.TagText() is { } path) Launcher.OpenClaudeCode(path);
    }

    private void StartLast_Click(object sender, RoutedEventArgs e)
    {
        var last = this.Vm()?.Claude.Projects.FirstOrDefault();
        if (last != null) Launcher.OpenClaudeCode(last.Path);
        else PickFolder_Click(sender, e);
    }

    private void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Claude Code'u hangi klasörde başlatayım?" };
        if (dialog.ShowDialog() == true)
            Launcher.OpenClaudeCode(dialog.FolderName);
    }
}
