using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland.Controls;

/// <summary>
/// A music progress bar you can click or drag to jump to a position.
///
/// While dragging, the bar follows the mouse and <see cref="PreviewChanged"/> reports the
/// position so the elapsed time can update live. On release, <see cref="SeekRequested"/>
/// asks the player to jump there. Values are 0–1 (fraction of the song).
/// </summary>
public sealed class SeekBar : FrameworkElement
{
    private const double ThumbRadius = 6;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(SeekBar),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(SeekBar),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Playback progress from 0 to 1.</summary>
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>False when the playing app doesn't support seeking; the bar then only shows progress.</summary>
    public bool IsInteractive { get => (bool)GetValue(IsInteractiveProperty); set => SetValue(IsInteractiveProperty, value); }

    /// <summary>Raised continuously while dragging.</summary>
    public event EventHandler<double>? PreviewChanged;

    /// <summary>Raised once when the user releases the mouse.</summary>
    public event EventHandler<double>? SeekRequested;

    private double? _dragValue;

    public SeekBar()
    {
        Height = 16;
        Cursor = Cursors.Hand;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double centerY = ActualHeight / 2;
        if (width <= 0) return;

        bool active = IsInteractive && (IsMouseOver || _dragValue.HasValue);
        double trackHeight = active ? 6 : 4;
        double value = Math.Clamp(_dragValue ?? Value, 0, 1);

        // Transparent background so the whole height is easy to grab
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var track = new Rect(0, centerY - trackHeight / 2, width, trackHeight);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), null,
            track, trackHeight / 2, trackHeight / 2);

        double filled = width * value;
        if (filled > 0)
        {
            dc.DrawRoundedRectangle(Brushes.White, null,
                new Rect(0, track.Y, filled, trackHeight), trackHeight / 2, trackHeight / 2);
        }

        // The thumb only appears on hover or while dragging
        if (active)
        {
            double x = Math.Clamp(filled, ThumbRadius, width - ThumbRadius);
            dc.DrawEllipse(Brushes.White, null, new Point(x, centerY), ThumbRadius, ThumbRadius);
        }
    }

    protected override void OnMouseEnter(MouseEventArgs e) => InvalidateVisual();
    protected override void OnMouseLeave(MouseEventArgs e) => InvalidateVisual();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsInteractive) return;

        CaptureMouse();
        UpdateDrag(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) UpdateDrag(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;

        // Take the value before releasing: ReleaseMouseCapture raises LostMouseCapture,
        // which would otherwise treat this as a cancelled drag
        var value = _dragValue;
        _dragValue = null;
        ReleaseMouseCapture();

        if (value is double fraction) SeekRequested?.Invoke(this, fraction);

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // Capture lost without a release (e.g. the window was deactivated): cancel the drag
        if (_dragValue.HasValue)
        {
            _dragValue = null;
            PreviewChanged?.Invoke(this, double.NaN);
            InvalidateVisual();
        }
    }

    private void UpdateDrag(Point point)
    {
        if (ActualWidth <= 0) return;

        _dragValue = Math.Clamp(point.X / ActualWidth, 0, 1);
        PreviewChanged?.Invoke(this, _dragValue.Value);
        InvalidateVisual();
    }
}
