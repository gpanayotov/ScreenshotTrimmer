using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// The crop rectangle being edited on an image, in whole image pixels and always inside the image, with the rules for
/// editing it through a <see cref="Viewport"/>. Has no interface dependency and knows nothing about where its rectangle
/// came from, so a Classic crop and a detected crop are edited the same way. Any edit that changes the rectangle gives
/// a Classic crop: the <b>Popup overhang</b> of a detected crop is dropped.
/// </summary>
public sealed record CropRectangle
{
    /// <summary>A drag shorter than this, in device-independent screen units, is a click.</summary>
    public const double ClickDistance = 4;

    /// <summary>
    /// How near a handle, in device-independent screen units, the pointer must be to grab it, so handles are as easy
    /// to grab at any zoom.
    /// </summary>
    public const double HandleTolerance = 6;

    private CropRectangle(PixelRect? rectangle, int imageWidth, int imageHeight)
    {
        Rectangle = rectangle;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
    }

    /// <summary>The crop rectangle in image pixels, or <c>null</c> for no crop (the whole image).</summary>
    public PixelRect? Rectangle
    {
        get;
        private init
        {
            field = value;
            // Whatever the rectangle came from, an edited one is a Classic crop.
            _unedited = null;
        }
    }

    /// <summary>The crop being edited, kept with its popups until the rectangle changes.</summary>
    private Crop? _unedited;

    /// <summary>
    /// The crop Step for this rectangle: the crop it was made from with <see cref="Editing"/> while the rectangle is
    /// unchanged, otherwise a Classic crop of <see cref="Rectangle"/>; <c>null</c> for no crop.
    /// </summary>
    public Crop? Crop => Rectangle is { } rectangle ? _unedited ?? Crop.Classic(rectangle) : null;

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    /// <summary>
    /// <paramref name="rectangle"/> on an image of the given size, clipped to the image; <c>null</c>, or a rectangle
    /// wholly outside the image, gives no crop.
    /// </summary>
    public static CropRectangle Of(PixelRect? rectangle, int imageWidth, int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(imageWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(imageHeight, 1);
        var clipped = rectangle?.Intersect(new PixelRect(0, 0, imageWidth, imageHeight));
        return new CropRectangle(clipped is { IsEmpty: false } ? clipped : null, imageWidth, imageHeight);
    }

    /// <summary>
    /// <paramref name="crop"/>'s window on an image of the given size, clipped to the image, for editing; <c>null</c>
    /// gives no crop. Its popups are kept until the rectangle is edited.
    /// </summary>
    public static CropRectangle Editing(Crop? crop, int imageWidth, int imageHeight)
    {
        var editing = Of(crop?.Window, imageWidth, imageHeight);
        if (editing.Rectangle is not null)
            editing._unedited = crop;
        return editing;
    }

    /// <summary>
    /// The rectangle drawn by dragging from <paramref name="from"/> to <paramref name="to"/>, in either order, on the
    /// image shown by <paramref name="viewport"/> at the display <paramref name="scaling"/>. Each edge snaps to the
    /// nearest whole image pixel, the rectangle is clipped to the image and is at least 1 x 1. A drag shorter than
    /// <see cref="ClickDistance"/> on screen, or one wholly outside the image, leaves the crop unchanged.
    /// </summary>
    public CropRectangle Draw(ViewPoint from, ViewPoint to, Viewport viewport, double scaling)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);

        if (Math.Abs(to.X - from.X) < ClickDistance && Math.Abs(to.Y - from.Y) < ClickDistance)
            return this;

        var (x1, y1) = viewport.ToImage(from.X * scaling, from.Y * scaling);
        var (x2, y2) = viewport.ToImage(to.X * scaling, to.Y * scaling);
        var (left, right) = Edges(x1, x2, ImageWidth);
        var (top, bottom) = Edges(y1, y2, ImageHeight);
        if (left is null || top is null)
            return this;
        return this with { Rectangle = new PixelRect(left.Value, top.Value, right - left.Value, bottom - top.Value) };
    }

    /// <summary>
    /// The handle under the view point <paramref name="point"/> on the image shown by <paramref name="viewport"/> at the
    /// display <paramref name="scaling"/>: a corner or an edge within <see cref="HandleTolerance"/> of it on screen,
    /// the nearer one where several are, else the inside, else <see cref="CropHandle.None"/>.
    /// </summary>
    public CropHandle HitTest(ViewPoint point, Viewport viewport, double scaling)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);
        if (Rectangle is not { } rectangle)
            return CropHandle.None;

        var x = point.X * scaling;
        var y = point.Y * scaling;
        var tolerance = HandleTolerance * scaling;
        var (left, top) = viewport.ToView(rectangle.X, rectangle.Y);
        var (right, bottom) = viewport.ToView(rectangle.Right, rectangle.Bottom);

        var vertical = NearerEdge(x, left, right, tolerance);
        var horizontal = NearerEdge(y, top, bottom, tolerance);
        var withinX = x >= left - tolerance && x <= right + tolerance;
        var withinY = y >= top - tolerance && y <= bottom + tolerance;
        return (vertical, horizontal) switch
        {
            (Edge.Low, Edge.Low) => CropHandle.TopLeft,
            (Edge.High, Edge.Low) => CropHandle.TopRight,
            (Edge.High, Edge.High) => CropHandle.BottomRight,
            (Edge.Low, Edge.High) => CropHandle.BottomLeft,
            (Edge.Low, _) when withinY => CropHandle.Left,
            (Edge.High, _) when withinY => CropHandle.Right,
            (_, Edge.Low) when withinX => CropHandle.Top,
            (_, Edge.High) when withinX => CropHandle.Bottom,
            _ when x > left && x < right && y > top && y < bottom => CropHandle.Inside,
            _ => CropHandle.None,
        };
    }

    /// <summary>
    /// The crop after dragging <paramref name="handle"/> from <paramref name="from"/> to <paramref name="to"/> on the
    /// image shown by <paramref name="viewport"/> at the display <paramref name="scaling"/>, and the handle the pointer
    /// holds afterwards. The handle's edges move by the drag's length in whole image pixels and stop at the image's
    /// border. An edge dragged past its opposite flips the rectangle, so the handle becomes the opposite one; dragged
    /// onto it, it stays one pixel away. The inside moves the whole rectangle without resizing it.
    /// </summary>
    public (CropRectangle Crop, CropHandle Handle) Drag(
        CropHandle handle, ViewPoint from, ViewPoint to, Viewport viewport, double scaling)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);
        if (Rectangle is not { } rectangle || handle == CropHandle.None)
            return (this, handle);

        var dx = (int)Math.Round((to.X - from.X) * scaling / viewport.Zoom);
        var dy = (int)Math.Round((to.Y - from.Y) * scaling / viewport.Zoom);
        return Moved(rectangle, handle, dx, dy);
    }

    /// <summary>
    /// The crop after nudging <paramref name="handle"/> by (<paramref name="dx"/>, <paramref name="dy"/>) image pixels,
    /// as the arrow keys do: an edge moves only along its own axis, a corner moves its two edges, and the inside the
    /// whole rectangle. Nudging stops at the image's border and never flips the rectangle nor makes it smaller than
    /// 1 x 1.
    /// </summary>
    public CropRectangle Nudge(CropHandle handle, int dx, int dy)
    {
        if (Rectangle is not { } rectangle || handle == CropHandle.None)
            return this;
        if (handle == CropHandle.Inside)
            return Moved(rectangle, handle, dx, dy).Crop;

        var (horizontal, vertical) = Sides(handle);
        var (left, right) = Nudged(rectangle.X, rectangle.Right, horizontal, dx, ImageWidth);
        var (top, bottom) = Nudged(rectangle.Y, rectangle.Bottom, vertical, dy, ImageHeight);
        return WithRectangle(new PixelRect(left, top, right - left, bottom - top));
    }

    /// <summary>
    /// The edges <paramref name="low"/> and <paramref name="high"/> after nudging the one <paramref name="edge"/> names
    /// by <paramref name="delta"/>, within 0..<paramref name="length"/> and at least one pixel apart.
    /// </summary>
    private static (int Low, int High) Nudged(int low, int high, Edge edge, int delta, int length) => edge switch
    {
        Edge.Low => (Math.Clamp(low + delta, 0, high - 1), high),
        Edge.High => (low, Math.Clamp(high + delta, low + 1, length)),
        _ => (low, high),
    };

    /// <summary>The crop after moving <paramref name="handle"/>'s edges of <paramref name="rectangle"/> by whole pixels.</summary>
    private (CropRectangle Crop, CropHandle Handle) Moved(PixelRect rectangle, CropHandle handle, int dx, int dy)
    {
        if (handle == CropHandle.Inside)
        {
            var x = Math.Clamp(rectangle.X + dx, 0, ImageWidth - rectangle.Width);
            var y = Math.Clamp(rectangle.Y + dy, 0, ImageHeight - rectangle.Height);
            return (WithRectangle(rectangle with { X = x, Y = y }), handle);
        }

        var (horizontal, vertical) = Sides(handle);
        var (left, right, flippedX) = (rectangle.X, rectangle.Right, false);
        if (horizontal == Edge.Low)
            (left, right, flippedX) = MoveEdge(rectangle.X, rectangle.Right, dx, ImageWidth);
        else if (horizontal == Edge.High)
            (left, right, flippedX) = MoveEdge(rectangle.Right, rectangle.X, dx, ImageWidth);
        var (top, bottom, flippedY) = (rectangle.Y, rectangle.Bottom, false);
        if (vertical == Edge.Low)
            (top, bottom, flippedY) = MoveEdge(rectangle.Y, rectangle.Bottom, dy, ImageHeight);
        else if (vertical == Edge.High)
            (top, bottom, flippedY) = MoveEdge(rectangle.Bottom, rectangle.Y, dy, ImageHeight);

        var after = Handle(Flip(horizontal, flippedX), Flip(vertical, flippedY));
        return (WithRectangle(new PixelRect(left, top, right - left, bottom - top)), after);
    }

    /// <summary>
    /// The edges after moving the edge at <paramref name="moving"/> by <paramref name="delta"/> within
    /// 0..<paramref name="length"/>, the other staying at <paramref name="fixedAt"/>, at least one pixel apart, and
    /// whether the moved edge crossed the other.
    /// </summary>
    private static (int Low, int High, bool Flipped) MoveEdge(int moving, int fixedAt, int delta, int length)
    {
        var moved = Math.Clamp(moving + delta, 0, length);
        if (moved == fixedAt)
            moved = moving < fixedAt ? fixedAt - 1 : fixedAt + 1;
        var flipped = (moving < fixedAt) != (moved < fixedAt);
        return (Math.Min(moved, fixedAt), Math.Max(moved, fixedAt), flipped);
    }

    /// <summary>This crop with <paramref name="rectangle"/>, or this crop itself when the rectangle is unchanged.</summary>
    private CropRectangle WithRectangle(PixelRect rectangle) =>
        rectangle == Rectangle ? this : this with { Rectangle = rectangle };

    /// <summary>Which vertical edge (left is low) and which horizontal edge (top is low) <paramref name="handle"/> moves.</summary>
    private static (Edge Horizontal, Edge Vertical) Sides(CropHandle handle) => handle switch
    {
        CropHandle.Left => (Edge.Low, Edge.None),
        CropHandle.Right => (Edge.High, Edge.None),
        CropHandle.Top => (Edge.None, Edge.Low),
        CropHandle.Bottom => (Edge.None, Edge.High),
        CropHandle.TopLeft => (Edge.Low, Edge.Low),
        CropHandle.TopRight => (Edge.High, Edge.Low),
        CropHandle.BottomRight => (Edge.High, Edge.High),
        CropHandle.BottomLeft => (Edge.Low, Edge.High),
        _ => (Edge.None, Edge.None),
    };

    private static Edge Flip(Edge edge, bool flipped) => !flipped ? edge : edge switch
    {
        Edge.Low => Edge.High,
        Edge.High => Edge.Low,
        _ => edge,
    };

    /// <summary>The handle that moves the vertical edge <paramref name="x"/> and the horizontal edge <paramref name="y"/>.</summary>
    private static CropHandle Handle(Edge x, Edge y) => (x, y) switch
    {
        (Edge.Low, Edge.None) => CropHandle.Left,
        (Edge.High, Edge.None) => CropHandle.Right,
        (Edge.None, Edge.Low) => CropHandle.Top,
        (Edge.None, Edge.High) => CropHandle.Bottom,
        (Edge.Low, Edge.Low) => CropHandle.TopLeft,
        (Edge.High, Edge.Low) => CropHandle.TopRight,
        (Edge.High, Edge.High) => CropHandle.BottomRight,
        (Edge.Low, Edge.High) => CropHandle.BottomLeft,
        _ => CropHandle.None,
    };

    /// <summary>Which of the edges <paramref name="low"/> and <paramref name="high"/> is within reach of <paramref name="at"/>.</summary>
    private static Edge NearerEdge(double at, double low, double high, double tolerance)
    {
        var toLow = Math.Abs(at - low);
        var toHigh = Math.Abs(at - high);
        if (toLow <= tolerance && toLow <= toHigh)
            return Edge.Low;
        return toHigh <= tolerance ? Edge.High : Edge.None;
    }

    private enum Edge
    {
        None,
        Low,
        High,
    }

    /// <summary>
    /// The edges, on whole pixels within 0..<paramref name="length"/>, of a span drawn from <paramref name="a"/> to
    /// <paramref name="b"/>, at least one pixel apart; <c>null</c> when the span lies wholly outside the image.
    /// </summary>
    private static (int? Start, int End) Edges(double a, double b, int length)
    {
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        if (high <= 0 || low >= length)
            return (null, 0);
        var start = Math.Clamp((int)Math.Round(low), 0, length);
        var end = Math.Clamp((int)Math.Round(high), 0, length);
        if (end > start)
            return (start, end);
        // Narrower than a pixel: keep the pixel the drag lies in.
        start = Math.Clamp((int)Math.Floor(low), 0, length - 1);
        return (start, start + 1);
    }
}
