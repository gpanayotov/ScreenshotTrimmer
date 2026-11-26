using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Viewing;

namespace ScreenshotTrimmer.App.Controls;

/// <summary>What the viewer draws behind the image, so transparency can be told from white.</summary>
public enum ViewerBackground
{
    Checkerboard,
    White,
    DarkGrey,
}

/// <summary>
/// Draws one image with zoom and pan. A zoom of 1 (100%) is one image pixel per physical screen pixel, whatever the
/// display scaling. Pixels are sharp squares from 100% up and smoothly sampled below it. Each new image starts fitted.
/// </summary>
/// <remarks>
/// Panning uses the middle button, the left button while <see cref="IsSpaceHeld"/>, Shift with the wheel and the
/// scroll properties; a plain left-drag is left to the caller. The wheel zooms around the cursor.
/// </remarks>
public sealed class ImageViewer : Control
{
    private const double KeyboardZoomStep = 1.25;
    private const double WheelZoomStep = 1.2;
    private const double WheelPanStep = 48;

    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ImageViewer, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<ViewerBackground> BackgroundKindProperty =
        AvaloniaProperty.Register<ImageViewer, ViewerBackground>(nameof(BackgroundKind));

    public static readonly DirectProperty<ImageViewer, double> ZoomProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(nameof(Zoom), viewer => viewer.Zoom);

    public static readonly DirectProperty<ImageViewer, double> HorizontalScrollProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(
            nameof(HorizontalScroll), viewer => viewer.HorizontalScroll, (viewer, value) => viewer.HorizontalScroll = value);

    public static readonly DirectProperty<ImageViewer, double> VerticalScrollProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(
            nameof(VerticalScroll), viewer => viewer.VerticalScroll, (viewer, value) => viewer.VerticalScroll = value);

    public static readonly DirectProperty<ImageViewer, double> HorizontalScrollMaxProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(nameof(HorizontalScrollMax), viewer => viewer.HorizontalScrollMax);

    public static readonly DirectProperty<ImageViewer, double> VerticalScrollMaxProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(nameof(VerticalScrollMax), viewer => viewer.VerticalScrollMax);

    public static readonly DirectProperty<ImageViewer, double> ViewWidthProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(nameof(ViewWidth), viewer => viewer.ViewWidth);

    public static readonly DirectProperty<ImageViewer, double> ViewHeightProperty =
        AvaloniaProperty.RegisterDirect<ImageViewer, double>(nameof(ViewHeight), viewer => viewer.ViewHeight);

    private static readonly IBrush Checkerboard = CreateCheckerboard();
    private static readonly IBrush DarkGrey = new ImmutableSolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));

    private Viewport? _viewport;
    private TopLevel? _topLevel;
    private Point? _panFrom;
    private bool _isSpaceHeld;
    private double _zoom;
    private double _horizontalScroll;
    private double _verticalScroll;
    private double _horizontalScrollMax;
    private double _verticalScrollMax;
    private double _viewWidth;
    private double _viewHeight;

    static ImageViewer()
    {
        AffectsRender<ImageViewer>(BackgroundKindProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ImageViewer>(true);
    }

    public ImageViewer()
    {
        FitCommand = new RelayCommand(() => Show(_viewport?.Fit()));
        ActualSizeCommand = new RelayCommand(() => ZoomAtCentre(viewport => viewport.ZoomAt(1, ViewWidth / 2, ViewHeight / 2)));
        ZoomInCommand = new RelayCommand(() => ZoomAtCentre(viewport => viewport.ZoomBy(KeyboardZoomStep, ViewWidth / 2, ViewHeight / 2)));
        ZoomOutCommand = new RelayCommand(() => ZoomAtCentre(viewport => viewport.ZoomBy(1 / KeyboardZoomStep, ViewWidth / 2, ViewHeight / 2)));
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ViewerBackground BackgroundKind
    {
        get => GetValue(BackgroundKindProperty);
        set => SetValue(BackgroundKindProperty, value);
    }

    /// <summary>Physical screen pixels per image pixel; 0 when there is no image.</summary>
    public double Zoom
    {
        get => _zoom;
        private set => SetAndRaise(ZoomProperty, ref _zoom, value);
    }

    /// <summary>How far the view is scrolled from the image's left edge, in physical screen pixels.</summary>
    public double HorizontalScroll
    {
        get => _horizontalScroll;
        set
        {
            if (_viewport is { } viewport && value != _horizontalScroll)
                Show(viewport.ScrollTo(value, viewport.VerticalScroll));
        }
    }

    /// <summary>How far the view is scrolled from the image's top edge, in physical screen pixels.</summary>
    public double VerticalScroll
    {
        get => _verticalScroll;
        set
        {
            if (_viewport is { } viewport && value != _verticalScroll)
                Show(viewport.ScrollTo(viewport.HorizontalScroll, value));
        }
    }

    public double HorizontalScrollMax
    {
        get => _horizontalScrollMax;
        private set => SetAndRaise(HorizontalScrollMaxProperty, ref _horizontalScrollMax, value);
    }

    public double VerticalScrollMax
    {
        get => _verticalScrollMax;
        private set => SetAndRaise(VerticalScrollMaxProperty, ref _verticalScrollMax, value);
    }

    /// <summary>The view's width in physical screen pixels, the scroll bars' viewport size.</summary>
    public double ViewWidth
    {
        get => _viewWidth;
        private set => SetAndRaise(ViewWidthProperty, ref _viewWidth, value);
    }

    /// <summary>The view's height in physical screen pixels, the scroll bars' viewport size.</summary>
    public double ViewHeight
    {
        get => _viewHeight;
        private set => SetAndRaise(ViewHeightProperty, ref _viewHeight, value);
    }

    /// <summary>Whether Space is held, so a left-drag pans. The window tracks the key, since the viewer has no focus.</summary>
    public bool IsSpaceHeld
    {
        get => _isSpaceHeld;
        set
        {
            _isSpaceHeld = value;
            if (_panFrom is null)
                Cursor = value ? new Cursor(StandardCursorType.Hand) : null;
        }
    }

    /// <summary>Shows the whole image, never enlarged past 100%.</summary>
    public ICommand FitCommand { get; }

    /// <summary>Shows the image at 100%, around the view's centre.</summary>
    public ICommand ActualSizeCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    private double Scaling => _topLevel?.RenderScaling ?? 1;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
            _topLevel.ScalingChanged += OnScalingChanged;
        Show(_viewport?.Resize(Bounds.Width * Scaling, Bounds.Height * Scaling));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
            _topLevel.ScalingChanged -= OnScalingChanged;
        _topLevel = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            Show(Source is { } bitmap
                ? Viewport.Fitted(bitmap.PixelSize.Width, bitmap.PixelSize.Height, Bounds.Width * Scaling, Bounds.Height * Scaling)
                : null);
        }
        else if (change.Property == BoundsProperty)
        {
            Show(_viewport?.Resize(Bounds.Width * Scaling, Bounds.Height * Scaling));
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_viewport is not { } viewport)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            // Some platforms turn Shift with the wheel into a horizontal delta themselves.
            var delta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
            Show(viewport.PanBy(delta * WheelPanStep * Scaling, 0));
        }
        else if (e.Delta.Y != 0)
        {
            var anchor = e.GetPosition(this) * Scaling;
            Show(viewport.ZoomBy(Math.Pow(WheelZoomStep, e.Delta.Y), anchor.X, anchor.Y));
        }
        else
        {
            Show(viewport.PanBy(e.Delta.X * WheelPanStep * Scaling, 0));
        }
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this).Properties;
        if (!point.IsMiddleButtonPressed && !(point.IsLeftButtonPressed && IsSpaceHeld))
            return;

        _panFrom = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_panFrom is not { } from || _viewport is not { } viewport)
            return;

        var position = e.GetPosition(this);
        var delta = (position - from) * Scaling;
        _panFrom = position;
        Show(viewport.PanBy(delta.X, delta.Y));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panFrom is null)
            return;
        e.Pointer.Capture(null);
        EndPan();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndPan();
    }

    public override void Render(DrawingContext context)
    {
        // Makes the whole view, not only the image, receive the wheel and the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Source is not { } bitmap || _viewport is not { } viewport)
            return;

        // The viewport works in physical pixels; drawing is in device-independent ones. Starting the image on a
        // whole physical pixel keeps every image pixel the same width at whole zooms.
        var scaling = Scaling;
        var destination = new Rect(
            Math.Round(viewport.ImageLeft) / scaling,
            Math.Round(viewport.ImageTop) / scaling,
            viewport.ShownWidth / scaling,
            viewport.ShownHeight / scaling);

        context.FillRectangle(BackgroundKind switch
        {
            ViewerBackground.White => Brushes.White,
            ViewerBackground.DarkGrey => DarkGrey,
            _ => Checkerboard,
        }, destination);

        // Sharp squares from 100% up, smooth downscaling below it.
        var interpolation = viewport.Zoom >= 1 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = interpolation }))
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), destination);
    }

    private void OnScalingChanged(object? sender, EventArgs e) =>
        Show(_viewport?.Resize(Bounds.Width * Scaling, Bounds.Height * Scaling));

    private void ZoomAtCentre(Func<Viewport, Viewport> zoom)
    {
        if (_viewport is { } viewport)
            Show(zoom(viewport));
    }

    private void EndPan()
    {
        _panFrom = null;
        Cursor = IsSpaceHeld ? new Cursor(StandardCursorType.Hand) : null;
    }

    /// <summary>Shows <paramref name="viewport"/> and publishes its zoom and scroll state.</summary>
    private void Show(Viewport? viewport)
    {
        _viewport = viewport;
        Zoom = viewport?.Zoom ?? 0;
        ViewWidth = Bounds.Width * Scaling;
        ViewHeight = Bounds.Height * Scaling;
        HorizontalScrollMax = viewport?.HorizontalScrollMax ?? 0;
        VerticalScrollMax = viewport?.VerticalScrollMax ?? 0;
        SetAndRaise(HorizontalScrollProperty, ref _horizontalScroll, viewport?.HorizontalScroll ?? 0);
        SetAndRaise(VerticalScrollProperty, ref _verticalScroll, viewport?.VerticalScroll ?? 0);
        InvalidateVisual();
    }

    /// <summary>A checkerboard of 8-pixel cells, fixed to the view rather than the image so it does not zoom.</summary>
    private static IBrush CreateCheckerboard()
    {
        var light = new ImmutableSolidColorBrush(Colors.White);
        var dark = new ImmutableSolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
        var cells = new DrawingGroup
        {
            Children =
            {
                new GeometryDrawing { Brush = light, Geometry = new RectangleGeometry(new Rect(0, 0, 16, 16)) },
                new GeometryDrawing { Brush = dark, Geometry = new RectangleGeometry(new Rect(0, 0, 8, 8)) },
                new GeometryDrawing { Brush = dark, Geometry = new RectangleGeometry(new Rect(8, 8, 8, 8)) },
            },
        };
        return new DrawingBrush(cells)
        {
            TileMode = TileMode.Tile,
            SourceRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
            DestinationRect = new RelativeRect(0, 0, 16, 16, RelativeUnit.Absolute),
        };
    }
}
