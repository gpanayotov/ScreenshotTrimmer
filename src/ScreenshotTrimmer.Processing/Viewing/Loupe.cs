using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// What the <b>Loupe</b> shows while a crop is drawn, dragged or nudged: the image pixel it aims at. The pixel is the
/// one the crop's edge lands on, the kept pixel next to the held handle, so its coordinates are the crop's own.
/// </summary>
public sealed record Loupe
{
    /// <summary>How many image pixels across the Loupe shows; odd, so the pixel aimed at is the middle one.</summary>
    public const int Span = 17;

    /// <summary>How far the Loupe stays from the point it is placed beside, in the units the view is measured in.</summary>
    public const double Gap = 20;
    private Loupe(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>The image x coordinate of the pixel aimed at, under the crosshair.</summary>
    public int X { get; }

    /// <summary>The image y coordinate of the pixel aimed at, under the crosshair.</summary>
    public int Y { get; }

    /// <summary>
    /// The image pixels the Loupe magnifies: a square <see cref="Span"/> pixels across centred on the pixel aimed at.
    /// Near the image's border part of it lies outside the image.
    /// </summary>
    public PixelRect Area => new(X - Span / 2, Y - Span / 2, Span, Span);

    /// <summary>
    /// The Loupe for <paramref name="crop"/> on the image shown by <paramref name="viewport"/> at the display
    /// <paramref name="scaling"/>, while <paramref name="handle"/> is held and the pointer is at the view point
    /// <paramref name="pointer"/> (<c>null</c> while nudging with the keys). A corner aims at the kept pixel in that
    /// corner; an edge at its kept pixel level with the pointer, or in its middle; drawing (<see cref="CropHandle.None"/>)
    /// and moving (<see cref="CropHandle.Inside"/>) at the corner nearest the pointer, or the top-left one. With no
    /// rectangle yet it aims at the pixel under the pointer, and without a pointer there is no Loupe.
    /// </summary>
    public static Loupe? At(CropRectangle crop, CropHandle handle, ViewPoint? pointer, Viewport viewport, double scaling)
    {
        ArgumentNullException.ThrowIfNull(crop);
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scaling);
        (double X, double Y)? at = pointer is { } p ? viewport.ToImage(p.X * scaling, p.Y * scaling) : null;
        if (crop.Rectangle is not { } rectangle)
        {
            return at is { } point
                ? new Loupe(Along(0, crop.ImageWidth, point.X), Along(0, crop.ImageHeight, point.Y))
                : null;
        }

        if (handle is CropHandle.None or CropHandle.Inside)
            handle = NearestCorner(rectangle, at);
        var x = handle switch
        {
            CropHandle.Left or CropHandle.TopLeft or CropHandle.BottomLeft => rectangle.X,
            CropHandle.Right or CropHandle.TopRight or CropHandle.BottomRight => rectangle.Right - 1,
            _ => Along(rectangle.X, rectangle.Width, at?.X),
        };
        var y = handle switch
        {
            CropHandle.Top or CropHandle.TopLeft or CropHandle.TopRight => rectangle.Y,
            CropHandle.Bottom or CropHandle.BottomLeft or CropHandle.BottomRight => rectangle.Bottom - 1,
            _ => Along(rectangle.Y, rectangle.Height, at?.Y),
        };
        return new Loupe(x, y);
    }

    /// <summary>
    /// Where the top-left corner of a Loupe <paramref name="width"/> x <paramref name="height"/> goes beside the view
    /// point <paramref name="anchor"/> (the pointer, or the nudged handle) in a view of the given size, all in the same
    /// units: <see cref="Gap"/> below and to the right of it, flipped to the other side on an axis where that would
    /// leave the view, and stopped at the view's edges where neither side has room.
    /// </summary>
    public static ViewPoint Place(ViewPoint anchor, double width, double height, double viewWidth, double viewHeight) =>
        new(Beside(anchor.X, width, viewWidth), Beside(anchor.Y, height, viewHeight));

    /// <summary>Where a span of <paramref name="size"/> starts beside <paramref name="anchor"/> within 0..<paramref name="length"/>.</summary>
    private static double Beside(double anchor, double size, double length)
    {
        var start = anchor + Gap;
        if (start + size > length)
            start = anchor - Gap - size;
        return Math.Max(0, Math.Min(start, length - size));
    }

    /// <summary>The corner of <paramref name="rectangle"/> nearest the image point <paramref name="at"/>; the top-left without one.</summary>
    private static CropHandle NearestCorner(PixelRect rectangle, (double X, double Y)? at)
    {
        if (at is not { } point)
            return CropHandle.TopLeft;
        var left = point.X < rectangle.X + rectangle.Width / 2.0;
        var top = point.Y < rectangle.Y + rectangle.Height / 2.0;
        return (left, top) switch
        {
            (true, true) => CropHandle.TopLeft,
            (false, true) => CropHandle.TopRight,
            (false, false) => CropHandle.BottomRight,
            (true, false) => CropHandle.BottomLeft,
        };
    }

    /// <summary>
    /// The pixel along a span of <paramref name="length"/> pixels from <paramref name="start"/> level with the pointer
    /// at <paramref name="pointer"/>, kept within the span; its middle pixel without a pointer.
    /// </summary>
    private static int Along(int start, int length, double? pointer) =>
        pointer is { } at ? Math.Clamp((int)Math.Floor(at), start, start + length - 1) : start + (length - 1) / 2;
}
