using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicIsland.Services;

namespace DynamicIsland.Views.Pages;

public sealed record TodoRow(string Id, string Text, double TextOpacity, TextDecorationCollection? Decorations,
                             Visibility CheckVisibility, Brush CheckFill, Brush CheckBorder);

/// <summary>
/// A simple to-do list: type a task and press Enter, click the circle to tick it off.
/// Open tasks are listed first, then completed ones. Saved to todos.json on every change.
/// </summary>
public partial class TodoPage : UserControl
{
    private static readonly Brush Cyan = new SolidColorBrush(Color.FromRgb(0x36, 0xB3, 0xD9));
    private static readonly Brush OpenBorder = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));

    private readonly List<TodoItem> _items;

    public TodoPage()
    {
        InitializeComponent();
        _items = TodoStore.Load();
        Render();
    }

    private void Render()
    {
        TaskList.ItemsSource = _items
            .OrderBy(i => i.Done)
            .ThenBy(i => i.Created)
            .Select(i => new TodoRow(
                i.Id,
                i.Text,
                i.Done ? 0.45 : 1,
                i.Done ? TextDecorations.Strikethrough : null,
                i.Done ? Visibility.Visible : Visibility.Collapsed,
                i.Done ? Cyan : Brushes.Transparent,
                i.Done ? Cyan : OpenBorder))
            .ToList();

        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        int done = _items.Count(i => i.Done);
        DoneCount.Text = done.ToString();
        TotalCount.Text = $"/{_items.Count}";

        double ratio = _items.Count == 0 ? 0 : (double)done / _items.Count;
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(ratio, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void SaveAndRender()
    {
        TodoStore.Save(_items);
        Render();
    }

    // ---------------------------------------------------------------- adding

    private void NewTask_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => StartTyping();

    private void StartTyping()
    {
        this.Island()?.BeginTextInput();
        NewTask.Focus();
        Keyboard.Focus(NewTask);
    }

    private void NewTask_TextChanged(object sender, TextChangedEventArgs e) =>
        Placeholder.Visibility = string.IsNullOrEmpty(NewTask.Text) ? Visibility.Visible : Visibility.Collapsed;

    private void NewTask_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        AddTask();
        e.Handled = true;
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTask.Text)) StartTyping();
        else AddTask();
    }

    private void AddTask()
    {
        var text = NewTask.Text.Trim();
        if (text.Length == 0) return;

        _items.Add(new TodoItem { Text = text });
        NewTask.Clear();
        SaveAndRender();
    }

    // ---------------------------------------------------------------- editing

    private void Toggle_Click(object sender, MouseButtonEventArgs e)
    {
        var item = _items.FirstOrDefault(i => i.Id == sender.TagText());
        if (item == null) return;

        item.Done = !item.Done;
        SaveAndRender();
        e.Handled = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _items.RemoveAll(i => i.Id == sender.TagText());
        SaveAndRender();
    }

    private void ClearDone_Click(object sender, RoutedEventArgs e)
    {
        _items.RemoveAll(i => i.Done);
        SaveAndRender();
    }
}
