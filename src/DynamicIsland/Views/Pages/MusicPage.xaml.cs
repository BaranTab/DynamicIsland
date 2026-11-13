using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicIsland.ViewModels;

namespace DynamicIsland.Views.Pages;

public partial class MusicPage : UserControl
{
    public MusicPage()
    {
        InitializeComponent();

        // Dragging the progress bar previews the time; releasing it jumps there
        Seek.PreviewChanged += (_, fraction) => this.Vm()?.PreviewSeek(fraction);
        Seek.SeekRequested += async (_, fraction) =>
        {
            if (this.Vm() is { } vm) await vm.SeekAsync(fraction);
        };

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is MainViewModel oldVm) oldVm.LyricLineChanged -= OnLyricLineChanged;
            if (e.NewValue is MainViewModel newVm) newVm.LyricLineChanged += OnLyricLineChanged;
        };
    }

    /// <summary>
    /// Spotify-style transition: the lines slide up a little while the new
    /// current line fades in, so the change feels like scrolling.
    /// </summary>
    private void OnLyricLineChanged(object? sender, EventArgs e)
    {
        var duration = TimeSpan.FromMilliseconds(320);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        LyricsShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(14, 0, duration) { EasingFunction = ease });

        CurrentLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0.35, 1, duration));
        NextLine.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 0.35, duration));
    }

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
