namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// Which part of an image a viewer shows and how large: the zoom and the pan, with no interface dependency.
/// Lengths in the view are in screen pixels, so a <see cref="Zoom"/> of 1 shows one image pixel per screen pixel.
/// </summary>
public sealed record Viewport
{
    /// <summary>The smallest zoom, 5%.</summary>
    public const double MinZoom = 0.05;

    /// <summary>The largest zoom, 3200%.</summary>
    public const double MaxZoom = 32;

    private Viewport(int imageWidth, int imageHeight, double viewWidth, double viewHeight)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        ViewWidth = viewWidth;
        ViewHeight = viewHeight;
    }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public double ViewWidth { get; private init; }

    public double ViewHeight { get; private init; }

    /// <summary>Screen pixels per image pixel.</summary>
    public double Zoom { get; private init; }

    /// <summary>The image x coordinate shown at the view's left edge.</summary>
    public double OriginX { get; private init; }

    /// <summary>The image y coordinate shown at the view's top edge.</summary>
    public double OriginY { get; private init; }

    /// <summary>Where the image's left edge is drawn in the view.</summary>
    public double ImageLeft => -OriginX * Zoom;

    /// <summary>Where the image's top edge is drawn in the view.</summary>
    public double ImageTop => -OriginY * Zoom;

    public double ShownWidth => ImageWidth * Zoom;

    public double ShownHeight => ImageHeight * Zoom;

    /// <summary>The whole image in a view of the given size, centred, shrunk to fit but never enlarged past 100%.</summary>
    public static Viewport Fitted(int imageWidth, int imageHeight, double viewWidth, double viewHeight) =>
        new Viewport(imageWidth, imageHeight, viewWidth, viewHeight).Fit();

    /// <summary>This viewport showing the whole image, centred, shrunk to fit but never enlarged past 100%.</summary>
    public Viewport Fit()
    {
        var zoom = Math.Min(1, Math.Min(ViewWidth / ImageWidth, ViewHeight / ImageHeight));
        return (this with { Zoom = zoom }).Clamped();
    }

    /// <summary>
    /// Zooms to <paramref name="zoom"/>, limited to <see cref="MinZoom"/>–<see cref="MaxZoom"/>, keeping the image
    /// point under the view point (<paramref name="anchorX"/>, <paramref name="anchorY"/>) in place where it can.
    /// </summary>
    public Viewport ZoomAt(double zoom, double anchorX, double anchorY)
    {
        zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        var imageX = OriginX + anchorX / Zoom;
        var imageY = OriginY + anchorY / Zoom;
        return (this with { Zoom = zoom, OriginX = imageX - anchorX / zoom, OriginY = imageY - anchorY / zoom })
            .Clamped();
    }

    /// <summary>
    /// Multiplies the zoom by <paramref name="factor"/> around the anchor, as <see cref="ZoomAt"/> does, but stops
    /// at 100% when the step would cross it, so stepping always passes through the true pixels.
    /// </summary>
    public Viewport ZoomBy(double factor, double anchorX, double anchorY)
    {
        var zoom = Zoom * factor;
        var crosses = (Zoom < 1 && zoom > 1) || (Zoom > 1 && zoom < 1);
        return ZoomAt(crosses ? 1 : zoom, anchorX, anchorY);
    }

    /// <summary>Moves the image by (<paramref name="dx"/>, <paramref name="dy"/>) screen pixels, within its edges.</summary>
    public Viewport PanBy(double dx, double dy) =>
        (this with { OriginX = OriginX - dx / Zoom, OriginY = OriginY - dy / Zoom }).Clamped();

    /// <summary>
    /// Centres the image on an axis where it is smaller than the view; elsewhere keeps the view covered by the image.
    /// </summary>
    private Viewport Clamped()
    {
        var visibleWidth = ViewWidth / Zoom;
        var visibleHeight = ViewHeight / Zoom;
        return this with
        {
            OriginX = ClampAxis(OriginX, ImageWidth, visibleWidth),
            OriginY = ClampAxis(OriginY, ImageHeight, visibleHeight),
        };
    }

    private static double ClampAxis(double origin, double imageLength, double visibleLength) =>
        imageLength <= visibleLength
            ? (imageLength - visibleLength) / 2
            : Math.Clamp(origin, 0, imageLength - visibleLength);
}
