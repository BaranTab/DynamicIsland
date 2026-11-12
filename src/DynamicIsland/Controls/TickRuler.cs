using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIsland.Controls;

/// <summary>
/// Odak zamanlayıcısı için cetvel görünümlü dakika seçici (0–60).
/// Sürükleyerek veya tıklayarak süre seçilir.
/// </summary>
public sealed class TickRuler : FrameworkElement
{
    private const double Padding = 10;

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(TickRuler),
        new FrameworkPropertyMetadata(25.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(TickRuler),
        new FrameworkPropertyMetadata(60.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <summary>Kullanıcı değeri değiştirdiğinde (dakika).</summary>
    public event EventHandler<double>? ValueChanged;

    protected override void OnRender(DrawingContext dc)
    {
        double width = ActualWidth;
        double usable = width - 2 * Padding;
        int max = (int)Maximum;
        if (usable <= 0 || max <= 0) return;

        // Tüm alan fareyi yakalasın
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));

        var minorPen = new Pen(new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)), 1);
        var majorPen = new Pen(new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)), 1.2);
        var labelBrush = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF));
        var typeface = new Typeface("Consolas");
        double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (int i = 0; i <= max; i++)
        {
            double x = Padding + usable * i / max;
            bool major = i % 10 == 0;
            bool mid = i % 5 == 0;
            double length = major ? 16 : mid ? 11 : 7;

            dc.DrawLine(major ? majorPen : minorPen, new Point(x, 6), new Point(x, 6 + length));

            if (major)
            {
                var text = new FormattedText(i.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, typeface, 10, labelBrush, pixelsPerDip);
                dc.DrawText(text, new Point(x - text.Width / 2, 27));
            }
        }

        // Seçili değer göstergesi
        double vx = Padding + usable * Math.Clamp(Value, 0, Maximum) / Maximum;
        dc.DrawRoundedRectangle(Brushes.White, null, new Rect(vx - 1.5, 0, 3, 28), 1.5, 1.5);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        CaptureMouse();
        UpdateFromPoint(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) UpdateFromPoint(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        e.Handled = true;
    }

    private void UpdateFromPoint(Point p)
    {
        double usable = ActualWidth - 2 * Padding;
        if (usable <= 0) return;

        double v = Math.Round((p.X - Padding) / usable * Maximum);
        v = Math.Clamp(v, 1, Maximum);

        if (Math.Abs(v - Value) > 0.001)
        {
            Value = v;
            ValueChanged?.Invoke(this, v);
        }
    }
}
