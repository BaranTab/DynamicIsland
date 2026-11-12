using System.Windows;
using System.Windows.Media;

namespace DynamicIsland.Controls;

/// <summary>
/// Ekranın üst kenarına "sıvı" gibi bağlanan cam görünümlü ada şekli.
///
///   ‾‾╲________________╱‾‾   ← üst köşeler içe kıvrılır (FilletRadius), ekran kenarına akar
///      |              |
///      ╰──────────────╯      ← alt köşeler yuvarlak (BottomRadius)
///
/// Cam efekti üç katmandan oluşur:
///   1) Koyu, hafif saydam taban (aşağı doğru vurgu rengine kayar)
///   2) Alttan yukarı doğru vuran renkli parıltı (GlowStrength)
///   3) Kenarlarda ince parlak çizgi (camın kenarı)
///
/// Tüm değerler DependencyProperty olduğu için animasyonla değiştirilebilir.
/// AccentColor bir ColorAnimation ile yumuşakça değiştirilir: "cam, çalan şeyin rengini alır".
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

        // 1) Cam tabanı: üstte neredeyse siyah, altta vurgu renginin koyu tonu
        var baseBrush = new LinearGradientBrush(
            Color.FromArgb(0xF4, 0x09, 0x09, 0x0C),
            Darken(accent, 0.62, alpha: 0xEA),
            90);
        baseBrush.Freeze();
        dc.DrawGeometry(baseBrush, null, geometry);

        // 2) Alttan gelen ışık
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

        // 3) Cam kenarı: üstte görünmez, aşağı doğru belirginleşen ince çizgi
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
            ctx.LineTo(new Point(right + f, 0), true, true);                                       // üst kenar
            ctx.ArcTo(new Point(right, f), new Size(f, f), 0, false, SweepDirection.Counterclockwise, true, true); // sağ üst sıvı geçiş
            ctx.LineTo(new Point(right, h - r), true, true);
            ctx.ArcTo(new Point(right - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);    // sağ alt
            ctx.LineTo(new Point(left + r, h), true, true);
            ctx.ArcTo(new Point(left, h - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, true);     // sol alt
            ctx.LineTo(new Point(left, f), true, true);
            ctx.ArcTo(new Point(left - f, 0), new Size(f, f), 0, false, SweepDirection.Counterclockwise, true, true); // sol üst sıvı geçiş
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
