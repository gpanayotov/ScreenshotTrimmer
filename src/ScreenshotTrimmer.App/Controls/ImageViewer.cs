using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Viewing;
using ImageRect = ScreenshotTrimmer.Processing.Imaging.PixelRect;

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
/// scroll properties. A plain left-drag draws the crop rectangle, shown with the image outside it dimmed; the drawn
/// rectangle is set as <see cref="Crop"/> when the button is released, so one drag is one change. The wheel zooms
/// around the cursor.
/// </remarks>
public sealed class ImageViewer : Control
{
    public static readonly StyledProperty<ImageRect?> CropProperty =
        AvaloniaProperty.Register<ImageViewer, ImageRect?>(nameof(Crop), defaultBindingMode: BindingMode.TwoWay);

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
    private static readonly IBrush CropDim = new ImmutableSolidColorBrush(Color.FromArgb(0x99, 0, 0, 0));
    private static readonly IPen CropOutline = new ImmutablePen(Brushes.White, 1);
    private static readonly IPen CropOutlineShade = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0)), 3);

    private Viewport? _viewport;
    private TopLevel? _topLevel;
    private Point? _panFrom;
    private ViewPoint? _drawFrom;
    private CropRectangle? _drawing;
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
        AffectsRender<ImageViewer>(BackgroundKindProperty, CropProperty);
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

    /// <summary>
    /// The crop rectangle drawn on the image, in image pixels, or <c>null</c> for none. A drawing drag sets it once,
    /// when the button is released.
    /// </summary>
    public ImageRect? Crop
    {
        get => GetValue(CropProperty);
        set => SetValue(CropProperty, value);
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
            EndDraw();
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
        if (point.IsLeftButtonPressed && !IsSpaceHeld && _panFrom is null && _drawFrom is null
            && Source is { } bitmap && _viewport is not null)
        {
            var position = e.GetPosition(this);
            _drawFrom = new ViewPoint(position.X, position.Y);
            _drawing = CropRectangle.Of(Crop, bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            e.Pointer.Capture(this);
            Cursor = new Cursor(StandardCursorType.Cross);
            e.Handled = true;
            return;
        }
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
        if (_drawFrom is not null)
        {
            _drawing = Drawn(e);
            InvalidateVisual();
            return;
        }
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
        if (_drawFrom is not null)
        {
            var drawn = Drawn(e)?.Rectangle;
            e.Pointer.Capture(null);
            EndDraw();
            if (drawn != Crop)
                SetCurrentValue(CropProperty, drawn);
            return;
        }
        if (_panFrom is null)
            return;
        e.Pointer.Capture(null);
        EndPan();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        // A drag cut short (another window took the pointer) leaves the crop as it was.
        EndDraw();
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
            viewport.DrawnLeft / scaling,
            viewport.DrawnTop / scaling,
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

        var crop = _drawFrom is not null ? _drawing?.Rectangle : Crop;
        if (crop is { } rectangle)
            DrawCrop(context, viewport, destination, rectangle);
    }

    /// <summary>Dims the image outside <paramref name="crop"/> and outlines it, on whole screen pixels.</summary>
    private void DrawCrop(DrawingContext context, Viewport viewport, Rect image, ImageRect crop)
    {
        var scaling = Scaling;
        var (left, top) = viewport.ToView(crop.X, crop.Y);
        var (right, bottom) = viewport.ToView(crop.Right, crop.Bottom);
        var inside = new Rect(
            new Point(Math.Round(left) / scaling, Math.Round(top) / scaling),
            new Point(Math.Round(right) / scaling, Math.Round(bottom) / scaling));

        context.FillRectangle(CropDim, new Rect(image.Left, image.Top, image.Width, inside.Top - image.Top));
        context.FillRectangle(CropDim, new Rect(image.Left, inside.Bottom, image.Width, image.Bottom - inside.Bottom));
        context.FillRectangle(CropDim, new Rect(image.Left, inside.Top, inside.Left - image.Left, inside.Height));
        context.FillRectangle(CropDim, new Rect(inside.Right, inside.Top, image.Right - inside.Right, inside.Height));

        // A light line with a dark edge either side shows on light and dark screenshots alike.
        context.DrawRectangle(null, CropOutlineShade, inside);
        context.DrawRectangle(null, CropOutline, inside);
    }

    /// <summary>The crop the drag in progress has drawn so far, up to the pointer position in <paramref name="e"/>.</summary>
    private CropRectangle? Drawn(PointerEventArgs e)
    {
        if (_drawFrom is not { } from || _drawing is not { } before || _viewport is not { } viewport)
            return _drawing;
        var position = e.GetPosition(this);
        var start = CropRectangle.Of(Crop, before.ImageWidth, before.ImageHeight);
        return start.Draw(from, new ViewPoint(position.X, position.Y), viewport, Scaling);
    }

    private void EndDraw()
    {
        if (_drawFrom is null)
            return;
        _drawFrom = null;
        _drawing = null;
        Cursor = IsSpaceHeld ? new Cursor(StandardCursorType.Hand) : null;
        InvalidateVisual();
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
