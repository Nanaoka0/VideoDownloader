using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace VideoDownloader.Controls;

/// <summary>
/// A tab-strip button whose rounded-rectangle shape is rebuilt from its bounds
/// (动态重算几何：顶部圆角 rTop=6、底部外凸弧 R=6；描边内缩半宽度防止被边界裁剪；
/// 选中时底边向下延伸 1px 盖住内容卡片顶边线。宽高任意变化不变形).
/// </summary>
public partial class TabButton : TemplatedControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<TabButton, object?>(nameof(Header));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<TabButton, bool>(nameof(IsSelected));

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<TabButton, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<TabButton, object?>(nameof(CommandParameter));

    public static readonly StyledProperty<Geometry?> GeometryProperty =
        AvaloniaProperty.Register<TabButton, Geometry?>(nameof(Geometry));

    public static new readonly StyledProperty<IBrush?> BorderBrushProperty =
        AvaloniaProperty.Register<TabButton, IBrush?>(nameof(BorderBrush));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<TabButton, double>(nameof(StrokeThickness), 1d);

    /// <summary>
    /// 基色填充（本地值，高于主题三态）：由 UpdateFill 派生基础色。
    /// 未设置 Fill 时回退主题三态样式。两套并存。
    /// </summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TabButton, IBrush?>(nameof(Fill));

    /// <summary>
    /// 是否绘制按钮边线（可由主题/样式单独设置）。
    /// </summary>
    public static readonly StyledProperty<bool> ShowBorderProperty =
        AvaloniaProperty.Register<TabButton, bool>(nameof(ShowBorder), true);

    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<TabButton, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public Geometry? Geometry
    {
        get => GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    public new IBrush? BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public bool ShowBorder
    {
        get => GetValue(ShowBorderProperty);
        set => SetValue(ShowBorderProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    private bool _pressed;
    private Rect _lastBounds;
    private double _lastThickness;
    private bool _lastSelected;

    static TabButton()
    {
        IsSelectedProperty.Changed.AddClassHandler<TabButton>((o, e) =>
        {
            o.UpdatePseudoClasses();
            o.RebuildGeometry();
            o.UpdateFill();
        });
        IsEnabledProperty.Changed.AddClassHandler<TabButton>((o, e) =>
        {
            o.UpdatePseudoClasses();
            o.UpdateFill();
        });
        BoundsProperty.Changed.AddClassHandler<TabButton>((o, _) => o.RebuildGeometry());
        StrokeThicknessProperty.Changed.AddClassHandler<TabButton>((o, _) => o.RebuildGeometry());
        FillProperty.Changed.AddClassHandler<TabButton>((o, _) => o.UpdateFill());
        IsPointerOverProperty.Changed.AddClassHandler<TabButton>((o, _) => o.UpdateFill());
    }

    public TabButton()
    {
        Focusable = true;
        UpdatePseudoClasses();
        RebuildGeometry();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        RebuildGeometry();
    }

    private void RebuildGeometry()
    {
        var b = Bounds;
        if (b.Width <= 0 || b.Height <= 0)
            return;

        if (_lastBounds == b && _lastThickness == StrokeThickness && _lastSelected == IsSelected)
            return;

        _lastBounds = b;
        _lastThickness = StrokeThickness;
        _lastSelected = IsSelected;

        const double R = 6.0;     // 底部外凸弧半径
        const double rTop = 6.0;  // 顶部圆角半径

        // 描边居中于几何边界，向内外各占一半；将整体内缩半描边宽度，
        // 使上/下边线完整落在组件边界内（不再被裁掉一半）。
        double inset = StrokeThickness / 2.0;
        double top = inset;
        double baseBottom = b.Height - inset;                // 外凸弧锚定的底边（已内缩半描边）
        double bottom = baseBottom + (IsSelected ? 1.0 : 0.0); // 选中时仅底边往下延伸

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            // 从左下角 (0, bottom) 开始（底边已延伸）
            c.BeginFigure(new Point(0, bottom), isFilled: true);

            // 左竖线向上到外凸弧起点 (0, baseBottom)
            c.LineTo(new Point(0, baseBottom));

            // 左下外凸弧（逆时针）：仍锚定在底边 (0, baseBottom) → (R, baseBottom - R)
            c.ArcTo(new Point(R, baseBottom - R),
                    new Size(R, R), 0, false, SweepDirection.CounterClockwise);

            // 左竖线：到 (R, rTop)
            c.LineTo(new Point(R, rTop));

            // 左上圆角（顺时针）：从 (R, rTop) 到 (R + rTop, top)
            c.ArcTo(new Point(R + rTop, top),
                    new Size(rTop, rTop), 0, false, SweepDirection.Clockwise);

            // 顶边：到 (b.Width - R - rTop, top)
            c.LineTo(new Point(b.Width - R - rTop, top));

            // 右上圆角（顺时针）：从 (b.Width - R - rTop, top) 到 (b.Width - R, rTop)
            c.ArcTo(new Point(b.Width - R, rTop),
                    new Size(rTop, rTop), 0, false, SweepDirection.Clockwise);

            // 右竖线：到 (b.Width - R, baseBottom - R)
            c.LineTo(new Point(b.Width - R, baseBottom - R));

            // 右下外凸弧（逆时针）：仍锚定在底边 (b.Width - R, baseBottom - R) → (b.Width, baseBottom)
            c.ArcTo(new Point(b.Width, baseBottom),
                    new Size(R, R), 0, false, SweepDirection.CounterClockwise);

            // 右竖线向下到延伸后的底边
            c.LineTo(new Point(b.Width, bottom));

            // 闭合底边（平直段），使底边线覆盖内容卡片顶边线，外凸弧保持原位置
            c.LineTo(new Point(0, bottom));
            c.EndFigure(isClosed: true);
        }

        Geometry = g;
    }

    /// <summary>
    /// 根据基色 Fill 与当前状态推导实际背景色并写到 Background（本地值高于主题三态样式，
    /// 因此自定义填充色时状态颜色在这里计算；未设置 Fill 则走主题样式）。
    /// 注意：仅支持纯色画刷（ISolidColorBrush）；渐变等画刷须由页面自行保证背景一致。
    /// </summary>
    private void UpdateFill()
    {
        if (Fill is not ISolidColorBrush solid)
            return;

        var c = solid.Color;
        var k = !IsEnabled ? 0.75
            : IsSelected ? 0d
            : _pressed ? 0.28
            : IsPointerOver ? 0.14
            : 0.52; // 未选中：向背景色淡化

        if (k > 0)
        {
            var dark = (string?)ActualThemeVariant?.Key == "Dark";
            var target = dark
                ? new Color(255, 20, 22, 26)
                : Colors.White;
            c = BlendToward(c, target, k);
        }

        SetValue(BackgroundProperty, new SolidColorBrush(c, solid.Opacity));
    }

    private static Color BlendToward(Color c, Color target, double k)
    {
        return new Color(255,
            (byte)(c.R + (target.R - c.R) * k),
            (byte)(c.G + (target.G - c.G) * k),
            (byte)(c.B + (target.B - c.B) * k));
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":selected", IsSelected);
        PseudoClasses.Set(":pressed", _pressed);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEffectivelyEnabled)
            return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressed = true;
            PseudoClasses.Set(":pressed", true);
            e.Handled = true;
            Focus();
            UpdateFill();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _pressed = false;
            PseudoClasses.Set(":pressed", false);
            UpdateFill();
            var p = e.GetPosition(this);
            // Bounds 是相对父级的坐标，命中判断要用控件自身坐标系（Size 即以自身原点）
            if (new Rect(Bounds.Size).Contains(p))
            {
                e.Handled = true;
                OnClick();
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Space or Key.Enter)
        {
            e.Handled = true;
            OnClick();
        }
    }

    protected virtual void OnClick()
    {
        if (!IsEffectivelyEnabled)
        {
            return;
        }

        RaiseEvent(new RoutedEventArgs(ClickEvent));
        if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }
}
