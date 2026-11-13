using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DynamicIsland.ViewModels;

namespace DynamicIsland.Views.Pages;

public partial class FocusPage : UserControl
{
    public FocusPage()
    {
        InitializeComponent();

        Ruler.ValueChanged += (_, minutes) => this.Vm()?.Focus.SetDuration(minutes);

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel oldVm) oldVm.PropertyChanged -= OnVmChanged;
            if (e.NewValue is MainViewModel newVm) newVm.PropertyChanged += OnVmChanged;
            SyncRuler();
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e) => SyncRuler();

    /// <summary>While running, the ruler shows the remaining time and is locked.</summary>
    private void SyncRuler()
    {
        if (this.Vm()?.Focus is not { } focus) return;

        if (!Ruler.IsMouseCaptured)
            Ruler.Value = focus.IsFinished ? focus.Duration.TotalMinutes : focus.Remaining.TotalMinutes;

        Ruler.IsHitTestVisible = !focus.IsRunning;
        Ruler.Opacity = focus.IsRunning ? 0.6 : 1;
    }

    private void StartPause_Click(object sender, RoutedEventArgs e) => this.Vm()?.Focus.ToggleStartPause();
    private void Reset_Click(object sender, RoutedEventArgs e) => this.Vm()?.Focus.Reset();
}
