using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DynamicIsland.Controls;
using DynamicIsland.Localization;
using DynamicIsland.Services;
using DynamicIsland.ViewModels;

namespace DynamicIsland.Views;

public partial class IslandWindow : Window
{
    // ---- Sizes ----
    private const double CompactHeight = 32, CompactRadius = 14, CompactFillet = 10;
    private const double ExpandedWidth = 680, ExpandedHeight = 290, ExpandedRadius = 36, ExpandedFillet = 22;

    // ---- Pages ----
    public const int PageHome = 0, PageMusic = 1, PageFocus = 2, PageWeather = 3, PageMail = 4,
                     PageClaude = 5, PageVsCode = 6, PageNotes = 7, PageCalendar = 8, PageCount = 9;
    private const double PageWidth = 636;

    // ---- Colours ----
    private static readonly Color NeutralAccent = Color.FromRgb(0x4A, 0x44, 0x58);
    private static readonly Color FocusAccent = Color.FromRgb(0x7B, 0x5C, 0xFF);

    /// <summary>Pages that have their own accent colour.</summary>
    private static readonly Dictionary<int, Color> PageAccents = new()
    {
        [PageFocus] = FocusAccent,
        [PageMail] = Color.FromRgb(0xE0, 0x57, 0x4F),
        [PageClaude] = Color.FromRgb(0xD9, 0x77, 0x57),
        [PageVsCode] = Color.FromRgb(0x2F, 0x80, 0xED),
        [PageNotes] = Color.FromRgb(0xE3, 0xB3, 0x41),
        [PageCalendar] = Color.FromRgb(0x3F, 0xB6, 0x8B)
    };

    private readonly MainViewModel _vm = new(App.Settings);
    private readonly DispatcherTimer _fullscreenTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _collapseTimer = new();
    private readonly SolidColorBrush _accentBrush = new(Lighten(NeutralAccent, 0.4));

    private bool _expanded;
    private int _page;
    private IslandMode _mode = IslandMode.Idle;
    private bool _equalizerPlaying;
    private Color _currentAccent = NeutralAccent;
    private double _currentGlow;
    private bool _textInput;

    // Dragging / scrolling
    private Point? _dragStart;
    private bool _dragging;
    private int _wheelAccumulator, _hWheelAccumulator;
    private DateTime _lastScrollPage = DateTime.MinValue;

    public IslandWindow()
    {
        InitializeComponent();

        Resources["AccentBrush"] = _accentBrush;
        DataContext = _vm;

        _vm.PropertyChanged += (_, _) => OnStateChanged();
        _vm.Focus.Completed += (_, _) => Dispatcher.InvokeAsync(OnFocusCompleted);
        _collapseTimer.Tick += CollapseTimer_Tick;
        Deactivated += (_, _) =>
        {
            // The user clicked somewhere else while typing a note
            if (!_textInput) return;
            EndTextInput();
            if (!IslandRoot.IsMouseOver) StartCollapseTimer();
        };

        BuildLanguageMenu();
        Loaded += OnLoaded;
    }

    /// <summary>Fills ⋯ → Language with the available languages and ticks the current one.</summary>
    private void BuildLanguageMenu()
    {
        LanguageMenu.Items.Clear();
        foreach (var language in Loc.Languages)
        {
            var item = new MenuItem
            {
                Header = language.NativeName,
                IsCheckable = true,
                IsChecked = language.Code == Loc.Instance.Language
            };
            item.Click += (_, _) =>
            {
                Loc.Instance.SetLanguage(language.Code);
                SettingsService.SaveLanguage(language.Code);
                BuildLanguageMenu();
            };
            LanguageMenu.Items.Add(item);
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.MakeToolWindow(this);

        // WPF has no event for horizontal touchpad scrolling (WM_MOUSEHWHEEL), so we hook it ourselves
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);

        // Stick to the top centre of the screen
        Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
        Top = 0;

        // Hide the island while a full-screen app is running
        _fullscreenTimer.Tick += (_, _) =>
            Visibility = WindowHelper.IsFullscreenAppRunning() ? Visibility.Hidden : Visibility.Visible;
        _fullscreenTimer.Start();

        UpdateNavigation(animate: false);
        await _vm.StartAsync();
    }

    // =====================================================================
    //  State changes: compact mode, equalizer, accent colour
    // =====================================================================

    private void OnStateChanged()
    {
        var mode = _vm.Mode;
        if (mode != _mode)
        {
            _mode = mode;
            if (!_expanded)
                AnimateShape(CompactWidthFor(mode), CompactHeight, CompactRadius, CompactFillet, growing: true);
        }

        bool eq = mode == IslandMode.Music && _vm.Media.IsPlaying;
        if (eq != _equalizerPlaying)
        {
            _equalizerPlaying = eq;
            SetEqualizer(eq);
        }

        UpdateAccent();
    }

    private static double CompactWidthFor(IslandMode mode) => mode switch
    {
        IslandMode.Music => 220,
        IslandMode.Timer => 200,
        _ => 150
    };

    private void SetEqualizer(bool playing)
    {
        int[] durations = { 320, 410, 270, 370 };
        int i = 0;
        foreach (var child in Equalizer.Children)
        {
            if (child is not FrameworkElement bar || bar.RenderTransform is not ScaleTransform scale) continue;

            if (playing)
            {
                var anim = new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(durations[i % durations.Length]))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }
            else
            {
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleY = 0.3;
            }
            i++;
        }
    }

    /// <summary>The glass takes the colour of whatever is in focus right now.</summary>
    private void UpdateAccent()
    {
        Color target;
        double glow;

        if (_expanded)
        {
            target = _page switch
            {
                _ when PageAccents.TryGetValue(_page, out var pageColor) => pageColor,
                PageWeather => _vm.WeatherAccent,
                _ when _vm.Media.IsActive && _vm.AlbumAccent is Color album => album,
                _ when _vm.Focus.IsRunning => FocusAccent,
                _ => NeutralAccent
            };
            glow = 1;
        }
        else
        {
            target = _mode switch
            {
                IslandMode.Timer => FocusAccent,
                IslandMode.Music => _vm.AlbumAccent ?? NeutralAccent,
                _ => NeutralAccent
            };
            glow = _mode == IslandMode.Idle ? 0 : 0.5;
        }

        if (target != _currentAccent)
        {
            _currentAccent = target;
            var duration = TimeSpan.FromMilliseconds(700);
            var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
            Glass.BeginAnimation(IslandShape.AccentColorProperty, new ColorAnimation(target, duration) { EasingFunction = ease });
            _accentBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Lighten(target, 0.4), duration) { EasingFunction = ease });
        }

        if (Math.Abs(glow - _currentGlow) > 0.01)
        {
            _currentGlow = glow;
            Glass.BeginAnimation(IslandShape.GlowStrengthProperty,
                new DoubleAnimation(glow, TimeSpan.FromMilliseconds(500)));
        }
    }

    private static Color Lighten(Color c, double t) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * t),
        (byte)(c.G + (255 - c.G) * t),
        (byte)(c.B + (255 - c.B) * t));

    // =====================================================================
    //  Expand / collapse
    // =====================================================================

    private void Island_MouseEnter(object sender, MouseEventArgs e)
    {
        _collapseTimer.Stop();
        Expand();
    }

    private void Island_MouseLeave(object sender, MouseEventArgs e) => StartCollapseTimer();

    private void StartCollapseTimer(int delayMs = 350)
    {
        _collapseTimer.Stop();
        _collapseTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
        _collapseTimer.Start();
    }

    private void CollapseTimer_Tick(object? sender, EventArgs e)
    {
        // Keep waiting while dragging, typing or a menu is open
        if (_textInput || Mouse.Captured != null || MoreButton.ContextMenu?.IsOpen == true) return;

        _collapseTimer.Stop();
        if (!IslandRoot.IsMouseOver) Collapse();
    }

    private void Expand()
    {
        if (_expanded) return;
        _expanded = true;

        AnimateShape(ExpandedWidth, ExpandedHeight, ExpandedRadius, ExpandedFillet, growing: true);

        Fade(CompactView, 0, 0);
        ExpandedView.Visibility = Visibility.Visible;
        Fade(ExpandedView, 1, 160);
        UpdateAccent();
    }

    private void Collapse()
    {
        if (!_expanded) return;
        _expanded = false;
        EndTextInput();

        AnimateShape(CompactWidthFor(_mode), CompactHeight, CompactRadius, CompactFillet, growing: false);

        Fade(ExpandedView, 0, 0, onCompleted: () =>
        {
            if (!_expanded) ExpandedView.Visibility = Visibility.Collapsed;
        });
        Fade(CompactView, 1, 200);
        UpdateAccent();
    }

    /// <summary>
    /// Growing: it first spreads sideways, then drops down with an elastic bounce.
    /// Shrinking: it first pulls up, then narrows.
    /// </summary>
    private void AnimateShape(double width, double height, double radius, double fillet, bool growing)
    {
        if (growing)
        {
            Animate(IslandShape.BodyWidthProperty, width, 420, 0, new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut });
            Animate(IslandShape.BodyHeightProperty, height, 520, 50, new ElasticEase { Oscillations = 1, Springiness = 7, EasingMode = EasingMode.EaseOut });
            Animate(IslandShape.BottomRadiusProperty, radius, 400, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
            Animate(IslandShape.FilletRadiusProperty, fillet, 400, 0, new CubicEase { EasingMode = EasingMode.EaseOut });
        }
        else
        {
            Animate(IslandShape.BodyHeightProperty, height, 320, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
            Animate(IslandShape.BodyWidthProperty, width, 380, 70, new BackEase { Amplitude = 0.25, EasingMode = EasingMode.EaseOut });
            Animate(IslandShape.BottomRadiusProperty, radius, 320, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
            Animate(IslandShape.FilletRadiusProperty, fillet, 320, 0, new CubicEase { EasingMode = EasingMode.EaseInOut });
        }
    }

    private void Animate(DependencyProperty property, double to, int durationMs, int delayMs, IEasingFunction ease)
    {
        Glass.BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = ease
        });
    }

    private static void Fade(UIElement element, double to, int delayMs, Action? onCompleted = null)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(180)) { BeginTime = TimeSpan.FromMilliseconds(delayMs) };
        if (onCompleted != null) anim.Completed += (_, _) => onCompleted();
        element.BeginAnimation(OpacityProperty, anim);
    }

    // =====================================================================
    //  Pages and tabs
    // =====================================================================

    public void GoToPage(int index)
    {
        _page = Math.Clamp(index, 0, PageCount - 1);

        // Refresh these lists when their page is opened
        if (_page == PageClaude) _ = _vm.Claude.RefreshAsync();
        else if (_page == PageVsCode) _ = _vm.VsCode.RefreshAsync();

        PagesTransform.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(-_page * PageWidth, TimeSpan.FromMilliseconds(420))
            {
                EasingFunction = new BackEase { Amplitude = 0.2, EasingMode = EasingMode.EaseOut }
            });

        UpdateNavigation(animate: true);
        UpdateAccent();
    }

    private void UpdateNavigation(bool animate)
    {
        var duration = TimeSpan.FromMilliseconds(animate ? 260 : 0);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        // Header dots: the active one stretches
        foreach (var child in Dots.Children)
        {
            if (child is not Border dot) continue;
            bool active = TagIndex(dot) == _page;
            dot.BeginAnimation(WidthProperty, new DoubleAnimation(active ? 14 : 5, duration) { EasingFunction = ease });
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(active ? 0.95 : 0.35, duration));
        }

        // Bottom tabs: the active one becomes a pill and shows its label
        foreach (var child in Tabs.Children)
        {
            if (child is not Border tab || tab.Child is not StackPanel content || content.Children.Count < 2) continue;
            bool active = TagIndex(tab) == _page;

            tab.Background = active ? new SolidColorBrush(Color.FromArgb(0x2A, 0xFF, 0xFF, 0xFF)) : Brushes.Transparent;
            content.Children[0].Opacity = active ? 1 : 0.55;

            if (content.Children[1] is TextBlock label)
            {
                label.Margin = new Thickness(active ? 8 : 0, 0, 0, 0);
                label.BeginAnimation(MaxWidthProperty, new DoubleAnimation(active ? 100 : 0, duration) { EasingFunction = ease });
                label.BeginAnimation(OpacityProperty, new DoubleAnimation(active ? 1 : 0, duration));
            }
        }
    }

    private static int TagIndex(FrameworkElement element) =>
        element.Tag is string s && int.TryParse(s, out int i) ? i : -1;

    /// <summary>Used by the tabs and the header dots.</summary>
    private void Navigate_Click(object sender, MouseButtonEventArgs e)
    {
        if (_dragging) return;
        if (sender is FrameworkElement element && TagIndex(element) is int index and >= 0)
            GoToPage(index);
    }

    // --- Mouse drag (like swiping on a phone) ---

    private void Viewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Dragging on the ruler or selecting text must not switch pages
        if (IsWithin<TickRuler>(e.OriginalSource) || IsWithin<TextBoxBase>(e.OriginalSource)) return;

        _dragStart = e.GetPosition(PagesViewport);
        _dragging = false;
    }

    private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not Point start || e.LeftButton != MouseButtonState.Pressed) return;

        double dx = e.GetPosition(PagesViewport).X - start.X;

        if (!_dragging && Math.Abs(dx) > 8)
        {
            _dragging = true;
            PagesViewport.CaptureMouse(); // Cancels the click on the button underneath
        }

        if (_dragging)
        {
            double x = -_page * PageWidth + dx;
            double min = -(PageCount - 1) * PageWidth;

            // "Rubber band" resistance at the edges
            if (x > 0) x *= 0.3;
            else if (x < min) x = min + (x - min) * 0.3;

            PagesTransform.BeginAnimation(TranslateTransform.XProperty, null);
            PagesTransform.X = x;
            e.Handled = true;
        }
    }

    private void Viewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragging && _dragStart is Point start)
        {
            double dx = e.GetPosition(PagesViewport).X - start.X;
            PagesViewport.ReleaseMouseCapture();

            if (dx < -PageWidth * 0.18) GoToPage(_page + 1);
            else if (dx > PageWidth * 0.18) GoToPage(_page - 1);
            else GoToPage(_page); // Spring back into place

            e.Handled = true;
            _dragStart = null;
            // Reset on the next frame so this release doesn't count as a click
            Dispatcher.InvokeAsync(() => _dragging = false, DispatcherPriority.Input);

            if (!IslandRoot.IsMouseOver) StartCollapseTimer();
            return;
        }

        _dragStart = null;
        _dragging = false;
    }

    private static bool IsWithin<T>(object? source) where T : DependencyObject
    {
        var current = source as DependencyObject;
        while (current != null)
        {
            if (current is T) return true;
            current = current is Visual
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return false;
    }

    // --- Mouse wheel ---

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _wheelAccumulator += e.Delta;
        if (Math.Abs(_wheelAccumulator) >= 120)
        {
            ScrollPage(_wheelAccumulator < 0 ? +1 : -1);
            _wheelAccumulator = 0;
        }
        e.Handled = true;
    }

    // --- Horizontal touchpad scrolling (two fingers left/right) ---

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_MOUSEHWHEEL = 0x020E;
        if (msg == WM_MOUSEHWHEEL && _expanded)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
            _hWheelAccumulator += delta;
            if (Math.Abs(_hWheelAccumulator) >= 120)
            {
                ScrollPage(_hWheelAccumulator > 0 ? +1 : -1);
                _hWheelAccumulator = 0;
            }
            handled = true;
            return new IntPtr(1);
        }
        return IntPtr.Zero;
    }

    /// <summary>Touchpads send events very quickly, so page changes are throttled.</summary>
    private void ScrollPage(int direction)
    {
        if ((DateTime.Now - _lastScrollPage).TotalMilliseconds < 300) return;
        _lastScrollPage = DateTime.Now;
        GoToPage(_page + direction);
    }

    // =====================================================================
    //  Buttons
    // =====================================================================

    private async void PlayPause_Click(object sender, RoutedEventArgs e) => await _vm.Media.TogglePlayPauseAsync();
    private async void Next_Click(object sender, RoutedEventArgs e) => await _vm.Media.NextAsync();
    private async void Previous_Click(object sender, RoutedEventArgs e) => await _vm.Media.PreviousAsync();

    private void FocusStartPause_Click(object sender, RoutedEventArgs e) => _vm.Focus.ToggleStartPause();
    private void FocusReset_Click(object sender, RoutedEventArgs e) => _vm.Focus.Reset();

    /// <summary>When the focus timer ends, the island opens, plays a sound and closes again after a few seconds.</summary>
    private void OnFocusCompleted()
    {
        SystemSounds.Asterisk.Play();
        Expand();
        GoToPage(PageFocus);
        StartCollapseTimer(5000);
    }

    private void More_Click(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not ContextMenu menu) return;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void Menu_Closed(object sender, RoutedEventArgs e)
    {
        if (!IslandRoot.IsMouseOver) StartCollapseTimer();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => Launcher.OpenSettingsFile();

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    // =====================================================================
    //  Keyboard input (Notes)
    //  The island normally never takes focus (WS_EX_NOACTIVATE). While typing a note we
    //  turn that off and activate the window; clicking elsewhere turns it back on.
    // =====================================================================

    public void BeginTextInput()
    {
        if (_textInput) return;
        _textInput = true;
        _collapseTimer.Stop();
        WindowHelper.SetNoActivate(this, false);
        Activate();
    }

    private void EndTextInput()
    {
        if (!_textInput) return;
        _textInput = false;
        Keyboard.ClearFocus();
        WindowHelper.SetNoActivate(this, true);
    }

    protected override void OnClosed(EventArgs e)
    {
        _fullscreenTimer.Stop();
        _collapseTimer.Stop();
        _vm.Dispose();
        base.OnClosed(e);
    }
}
