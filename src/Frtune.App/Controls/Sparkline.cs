using System.Windows;
using System.Windows.Media;

namespace Frtune.App.Controls;

/// <summary>Lightweight live usage graph (0–100). Drawn directly in OnRender: no chart library, no allocations per point.</summary>
public sealed class Sparkline : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline), new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush Stroke
    {
        get => (Brush)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var v = Values;
        if (v is null || v.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
        double w = ActualWidth, h = ActualHeight, step = w / (v.Count - 1);
        Point P(int i) => new(i * step, h - Math.Clamp(v[i], 0, 100) / 100 * (h - 2) - 1);

        var line = new StreamGeometry();
        using (var g = line.Open())
        {
            g.BeginFigure(P(0), false, false);
            for (var i = 1; i < v.Count; i++) g.LineTo(P(i), true, true);
        }
        line.Freeze();

        var area = new StreamGeometry();
        using (var g = area.Open())
        {
            g.BeginFigure(new Point(0, h), true, true);
            for (var i = 0; i < v.Count; i++) g.LineTo(P(i), true, true);
            g.LineTo(new Point(w, h), true, true);
        }
        area.Freeze();

        var color = (Stroke as SolidColorBrush)?.Color ?? Colors.MediumPurple;
        var fill = new LinearGradientBrush(Color.FromArgb(90, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B), 90);
        fill.Freeze();
        dc.DrawGeometry(fill, null, area);
        var pen = new Pen(Stroke, 1.6) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, line);
    }
}

/// <summary>Circular score gauge (0–100) for the health score.</summary>
public sealed class RingGauge : FrameworkElement
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(RingGauge), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(RingGauge), new FrameworkPropertyMetadata(Brushes.MediumPurple, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        const double thickness = 10;
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= thickness) return;
        var r = (size - thickness) / 2;
        var c = new Point(ActualWidth / 2, ActualHeight / 2);
        var track = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), thickness);
        dc.DrawEllipse(null, track, c, r, r);

        var frac = Math.Clamp(Value, 0, 100) / 100;
        if (frac <= 0) return;
        if (frac >= 0.999) { dc.DrawEllipse(null, new Pen(Stroke, thickness), c, r, r); return; }
        var angle = frac * 2 * Math.PI;
        var start = new Point(c.X, c.Y - r);
        var end = new Point(c.X + r * Math.Sin(angle), c.Y - r * Math.Cos(angle));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(r, r), 0, angle > Math.PI, SweepDirection.Clockwise, true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, new Pen(Stroke, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, g);
    }
}
