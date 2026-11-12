using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DynamicIsland.Services;

namespace DynamicIsland.Views.Pages;

public sealed record NoteRow(string Id, string Title, string Detail, Brush Background);

/// <summary>
/// Hızlı not defteri. Yazdıkça 0.6 sn sonra otomatik kaydeder.
/// Ada normalde odak almaz; yazmaya başlayınca pencere geçici olarak klavye alabilir hale gelir.
/// </summary>
public partial class NotesPage : UserControl
{
    private static readonly Brush ActiveRow = new SolidColorBrush(Color.FromArgb(0x38, 0xF2, 0xC1, 0x4E));
    private static readonly Brush InactiveRow = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));

    private readonly List<Note> _notes;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private Note _current;
    private bool _loading;

    public NotesPage()
    {
        InitializeComponent();

        _notes = NotesStore.Load();
        if (_notes.Count == 0) _notes.Add(new Note());
        _current = _notes.OrderByDescending(n => n.Updated).First();

        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            NotesStore.Save(_notes);
            SavedText.Text = $"kaydedildi {DateTime.Now:HH:mm}";
            RenderList();
        };

        LoadCurrent();
    }

    private void LoadCurrent()
    {
        _loading = true;
        Editor.Text = _current.Text;
        Editor.CaretIndex = Editor.Text.Length;
        _loading = false;

        UpdatePlaceholder();
        RenderList();
    }

    private void RenderList()
    {
        NotesList.ItemsSource = _notes
            .Where(n => n == _current || !string.IsNullOrWhiteSpace(n.Text))
            .OrderByDescending(n => n.Updated)
            .Take(4)
            .Select(n => new NoteRow(n.Id, n.Title, Format.Relative(n.Updated), n == _current ? ActiveRow : InactiveRow))
            .ToList();
    }

    private void UpdatePlaceholder() =>
        Placeholder.Visibility = string.IsNullOrEmpty(Editor.Text) ? Visibility.Visible : Visibility.Collapsed;

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePlaceholder();
        if (_loading) return;

        _current.Text = Editor.Text;
        _current.Updated = DateTime.Now;
        SavedText.Text = "yazılıyor…";

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Editor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => StartTyping();

    private void StartTyping()
    {
        this.Island()?.BeginTextInput();
        Editor.Focus();
        Keyboard.Focus(Editor);
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        // Zaten boş bir not açıksa yenisini açma
        if (!string.IsNullOrWhiteSpace(_current.Text))
        {
            _current = new Note();
            _notes.Add(_current);
            LoadCurrent();
        }
        StartTyping();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        _notes.Remove(_current);
        if (_notes.Count == 0) _notes.Add(new Note());
        _current = _notes.OrderByDescending(n => n.Updated).First();

        NotesStore.Save(_notes);
        SavedText.Text = "silindi";
        LoadCurrent();
    }

    private void Note_Click(object sender, MouseButtonEventArgs e)
    {
        var note = _notes.FirstOrDefault(n => n.Id == sender.TagText());
        if (note == null || note == _current) return;

        _current = note;
        LoadCurrent();
    }
}
