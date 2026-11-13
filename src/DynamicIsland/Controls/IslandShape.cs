using System.Windows;
using System.Windows.Media;

namespace DynamicIsland.Controls;

/// <summary>
/// The glass island shape that flows "liquid-like" out of the top edge of the screen.
///
///   ‾‾╲________________╱‾‾   ← top corners curve inwards (FilletRadius) into the screen edge
///      |              |
///      ╰──────────────╯      ← rounded bottom corners (BottomRadius)
///
/// The glass effect has three layers:
///   1) A dark, slightly translucent base (fading into the accent colour at the bottom)
///   2) A coloured glow shining up from below (GlowStrength)
///   3) A thin bright line along the edges (the edge of the glass)
///
/// Every value is a DependencyProperty, so all of them can be animated.
/// AccentColor is changed smoothly with a ColorAnimation: "the glass takes the colour of what's playing".
/// </summary>
public sealed class IslandShape : FrameworkElement
{
    public static readonly DependencyProperty BodyWidthProperty = RegisterDouble(nameof(BodyWidth), 150);
    public static readonly DependencyProperty BodyHeightProperty = RegisterDouble(nameof(BodyHeight), 32);
    public static readonly DependencyProperty BottomRadiusProperty = RegisterDouble(nameof(BottomRadius), 14);
    public static readonly DependencyProperty FilletRadiusProperty = RegisterDouble(nameof(FilletRadius), 10);
    public static readonly DependencyProperty GlowStrengthProperty = RegisterDouble(nameof(GlowStrength), 0);

    public static readonly DependencyProperty AccentColorProperty = DependencyProperty.Register(
        nameof(AccentColor), typeof(Color), typeof(IslandShape),
        new FrameworkPropertyMetadata(Color.FromRgb(0x4A, 0x44, 0x58), FrameworkPropertyMetadataOptions.AffectsRender));

    public double BodyWidth { get => (double)GetValue(BodyWidthProperty); set => SetValue(BodyWidthProperty, value); }
    public double BodyHeight { get => (double)GetValue(BodyHeightProperty); set => SetValue(BodyHeightProperty, value); }
    public double BottomRadius { get => (double)GetValue(BottomRadiusProperty); set => SetValue(BottomRadiusProperty, value); }
    public double FilletRadius { get => (double)GetValue(FilletRadiusProperty); set => SetValue(FilletRadiusProperty, value); }
    public double GlowStrength { get => (double)GetValue(GlowStrengthProperty); set => SetValue(GlowStrengthProperty, value); }
    public Color AccentColor { get => (Color)GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

    private static DependencyProperty RegisterDouble(string name, double defaultValue) =>
        DependencyProperty.Register(name, typeof(double), typeof(IslandShape),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));

    protected override void OnRender(DrawingContext dc)
    {
        var geometry = BuildGeometry();
        var accent = AccentColor;
        double glow = Math.Clamp(GlowStrength, 0, 1);

        // 1) Glass base: almost black at the top, a dark shade of the accent at the bottom
        var baseBrush = new LinearGradientBrush(
            Color.FromArgb(0xF4, 0x09, 0x09, 0x0C),
            Darken(accent, 0.62, alpha: 0xEA),
            90);
        baseBrush.Freeze();
        dc.DrawGeometry(baseBrush, null, geometry);

        // 2) Light coming up through the glass
        if (glow > 0.01)
        {
            var glowBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 1.25),
                GradientOrigin = new Point(0.5, 1.25),
                RadiusX = 0.8,
                RadiusY = 1.1
            };
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(170 * glow), accent.R, accent.G, accent.B), 0));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(60 * glow), accent.R, accent.G, accent.B), 0.55));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, accent.R, accent.G, accent.B), 1));
            glowBrush.Freeze();
            dc.DrawGeometry(glowBrush, null, geometry);
        }

        // 3) Glass edge: invisible at the top, a thin line that gets stronger towards the bottom
        var edgeBrush = new LinearGradientBrush(
            Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF),
            Color.FromArgb((byte)(0x30 + 0x30 * glow), 0xFF, 0xFF, 0xFF),
            90);
        edgeBrush.Freeze();
        var edgePen = new Pen(edgeBrush, 1);
        edgePen.Freeze();
        dc.DrawGeometry(null, edgePen, geometry);
    }

    private StreamGeometry BuildGeometry()
    {
        double w = Math.Max(0, BodyWidth);
        double h = Math.Max(0, BodyHeight);
        double f = Math.Max(0, FilletRadius);
        double r = Math.Max(0, Math.Min(BottomRadius, Math.Min(w / 2, h - f)));

        double left = (ActualWidth - w) / 2;
        double right = left + w;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(left - f, 0), isFilled: true, isClosed: true);
            ctx.LineTo(new Point(right + f, 0), true, true);                                       // top edge
            ctx.ArcTo(new Point(right, f), new Size(f, f), 0, false, SweepDirection.Counterclockwise, true, true); // top-right liquid curve
            ctx.LineTo(new Point(right, h - r), true, true);
            ctx.ArcTo(new Point(right - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);    // bottom-right
            ctx.LineTo(new Point(left + r, h), true, true);
            ctx.ArcTo(new Point(left, h - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);     // bottom-left
            ctx.LineTo(new Point(left, f), true, true);
            ctx.ArcTo(new Point(left - f, 0), new Size(f, f), 0, false, SweepDirection.Counterclockwise, true, true); // top-left liquid curve
        }
        geometry.Freeze();
        return geometry;
    }

    private static Color Darken(Color c, double amount, byte alpha) => Color.FromArgb(
        alpha,
        (byte)(c.R * (1 - amount)),
        (byte)(c.G * (1 - amount)),
        (byte)(c.B * (1 - amount)));
}
