using System.Windows.Input;
using System.Windows.Media;

namespace ProjectAgil.Controls;

public sealed class SparklineChart : FrameworkElement
{
    private static readonly Typeface LabelTypeface = new("Segoe UI");

    private readonly Dictionary<string, FormattedText> _labels = [];

    private Pen? _linePen;
    private Pen? _gridPen;
    private Brush? _cachedLineBrush;
    private Brush? _cachedGridBrush;
    private double _cachedThickness;
    private double _pixelsPerDip = 1d;
    private int _hoverIndex = -1;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<double>),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(
        nameof(LineBrush),
        typeof(Brush),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty AreaBrushProperty = DependencyProperty.Register(
        nameof(AreaBrush),
        typeof(Brush),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty GridBrushProperty = DependencyProperty.Register(
        nameof(GridBrush),
        typeof(Brush),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty LossBrushProperty = DependencyProperty.Register(
        nameof(LossBrush),
        typeof(Brush),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(Brushes.OrangeRed, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty LineThicknessProperty = DependencyProperty.Register(
        nameof(LineThickness),
        typeof(double),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(1.8d, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid),
        typeof(bool),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty ShowScaleProperty = DependencyProperty.Register(
        nameof(ShowScale),
        typeof(bool),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public static readonly DependencyProperty ScaleBrushProperty = DependencyProperty.Register(
        nameof(ScaleBrush),
        typeof(Brush),
        typeof(SparklineChart),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public Brush? AreaBrush
    {
        get => (Brush?)GetValue(AreaBrushProperty);
        set => SetValue(AreaBrushProperty, value);
    }

    public Brush? GridBrush
    {
        get => (Brush?)GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush LossBrush
    {
        get => (Brush)GetValue(LossBrushProperty);
        set => SetValue(LossBrushProperty, value);
    }

    public double LineThickness
    {
        get => (double)GetValue(LineThicknessProperty);
        set => SetValue(LineThicknessProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ShowScale
    {
        get => (bool)GetValue(ShowScaleProperty);
        set => SetValue(ShowScaleProperty, value);
    }

    public Brush ScaleBrush
    {
        get => (Brush)GetValue(ScaleBrushProperty);
        set => SetValue(ScaleBrushProperty, value);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var values = Values;
        var width = ActualWidth;

        if (values is null || values.Count < 2 || width <= 4)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        var step = width / (values.Count - 1);
        var index = Math.Clamp((int)Math.Round(x / step), 0, values.Count - 1);

        if (index != _hoverIndex)
        {
            _hoverIndex = index;
            InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        if (_hoverIndex >= 0)
        {
            _hoverIndex = -1;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext context)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 4 || height <= 4)
        {
            return;
        }

        _pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));

        var gridPen = GridPen();

        if (ShowGrid && gridPen is not null)
        {
            for (var i = 1; i < 4; i++)
            {
                var y = Math.Round(height * i / 4d) + 0.5;
                context.DrawLine(gridPen, new Point(0, y), new Point(width, y));
            }
        }

        var values = Values;

        if (values is null || values.Count < 2)
        {
            return;
        }

        var count = values.Count;
        var min = double.MaxValue;
        var max = double.MinValue;
        var good = 0;

        for (var i = 0; i < count; i++)
        {
            var value = values[i];

            if (value < 0)
            {
                continue;
            }

            good++;

            if (value < min)
            {
                min = value;
            }

            if (value > max)
            {
                max = value;
            }
        }

        if (good < 2)
        {
            return;
        }

        var span = max - min;

        if (span < 4)
        {
            var centre = (max + min) / 2d;
            min = Math.Max(0, centre - 2);
            max = centre + 2;
            span = max - min;
        }
        else
        {
            min = Math.Max(0, min - (span * 0.15));
            max += span * 0.15;
            span = max - min;
        }

        var step = width / (count - 1);
        var scale = height / span;

        double MapY(double value) => height - ((value - min) * scale);

        if (AreaBrush is not null)
        {
            var area = new StreamGeometry();

            using (var stream = area.Open())
            {
                var opened = false;
                var firstX = 0d;
                var lastX = 0d;

                for (var i = 0; i < count; i++)
                {
                    var value = values[i];

                    if (value < 0)
                    {
                        continue;
                    }

                    var point = new Point(i * step, MapY(value));

                    if (!opened)
                    {
                        firstX = point.X;
                        stream.BeginFigure(new Point(point.X, height), true, true);
                        stream.LineTo(point, true, true);
                        opened = true;
                    }
                    else
                    {
                        stream.LineTo(point, true, true);
                    }

                    lastX = point.X;
                }

                if (opened)
                {
                    stream.LineTo(new Point(lastX, height), true, true);
                    stream.LineTo(new Point(firstX, height), true, true);
                }
            }

            area.Freeze();
            context.DrawGeometry(AreaBrush, null, area);
        }

        var geometry = new StreamGeometry();

        using (var stream = geometry.Open())
        {
            var started = false;

            for (var i = 0; i < count; i++)
            {
                var value = values[i];

                if (value < 0)
                {
                    continue;
                }

                var point = new Point(i * step, MapY(value));

                if (!started)
                {
                    stream.BeginFigure(point, false, false);
                    started = true;
                }
                else
                {
                    stream.LineTo(point, true, true);
                }
            }
        }

        geometry.Freeze();
        context.DrawGeometry(null, LinePen(), geometry);

        for (var i = 0; i < count; i++)
        {
            if (values[i] >= 0)
            {
                continue;
            }

            context.DrawRectangle(LossBrush, null, new Rect(Math.Max(0, (i * step) - 1), 0, 2, height));
        }

        if (ShowScale)
        {
            DrawLabel(context, $"{max:0} ms", new Point(2, 2));
            DrawLabel(context, $"{min:0} ms", new Point(2, height - 15));
        }

        DrawHover(context, values, width, height, step, MapY);
    }

    private void DrawHover(
        DrawingContext context,
        IReadOnlyList<double> values,
        double width,
        double height,
        double step,
        Func<double, double> mapY
    )
    {
        if (_hoverIndex < 0 || _hoverIndex >= values.Count)
        {
            return;
        }

        var value = values[_hoverIndex];
        var x = Math.Clamp(_hoverIndex * step, 0, width);
        var pen = GridPen();

        if (pen is not null)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, height));
        }

        var text = value < 0 ? "no reply" : $"{value:0.#} ms";
        var label = Label(text, value < 0 ? ScaleBrush : LineBrush);

        if (value >= 0)
        {
            context.DrawEllipse(LineBrush, null, new Point(x, mapY(value)), 3.5, 3.5);
        }

        var boxWidth = label.Width + 14;
        var boxHeight = label.Height + 8;
        var boxX = x + 12 + boxWidth > width ? x - 12 - boxWidth : x + 12;

        context.DrawRoundedRectangle(HoverFill, HoverStroke, new Rect(boxX, 6, boxWidth, boxHeight), 4, 4);
        context.DrawText(label, new Point(boxX + 7, 10));
    }

    private static Brush HoverFill { get; } = Frozen(new SolidColorBrush(Color.FromArgb(0xEE, 0x1C, 0x1C, 0x1C)));

    private static Pen HoverStroke { get; } =
        Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))), 1));

    private static T Frozen<T>(T value)
        where T : Freezable
    {
        value.Freeze();
        return value;
    }

    private Pen? GridPen()
    {
        if (GridBrush is null)
        {
            return null;
        }

        if (_gridPen is null || !ReferenceEquals(_cachedGridBrush, GridBrush))
        {
            _cachedGridBrush = GridBrush;
            _gridPen = new Pen(GridBrush, 1) { DashStyle = new DashStyle([3, 4], 0) };
            _gridPen.Freeze();
        }

        return _gridPen;
    }

    private Pen LinePen()
    {
        if (_linePen is null
            || !ReferenceEquals(_cachedLineBrush, LineBrush)
            || Math.Abs(_cachedThickness - LineThickness) > 0.01)
        {
            _cachedLineBrush = LineBrush;
            _cachedThickness = LineThickness;

            _linePen = new Pen(LineBrush, LineThickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };

            _linePen.Freeze();
        }

        return _linePen;
    }

    private FormattedText Label(string text, Brush brush)
    {
        var key = $"{text}|{brush.GetHashCode()}";

        if (_labels.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_labels.Count > 256)
        {
            _labels.Clear();
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            LabelTypeface,
            11,
            brush,
            _pixelsPerDip
        );

        _labels[key] = formatted;
        return formatted;
    }

    private void DrawLabel(DrawingContext context, string text, Point origin) =>
        context.DrawText(Label(text, ScaleBrush), origin);
}
