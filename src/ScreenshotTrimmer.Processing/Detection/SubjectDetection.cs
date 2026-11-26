using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// Finds the <b>Subject</b> of a screenshot with classical image analysis only (ADR 0001): no network, no randomness and
/// only integer arithmetic, so the same image always gives the same Subject on any machine.
/// </summary>
public static class SubjectDetection
{
    /// <summary>
    /// What the whole image holds: the Subject to crop to, that the image is <b>Already cropped</b> to its Subject, or
    /// nothing found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both methods run. The rectangle method finds the windows: rectangles that cast an elevation shadow. The brightness
    /// method finds the bright area of a light dialog over a page dimmed by a modal backdrop, which keeps the dialog's
    /// exact edges. Its dialog is the Subject only when it passes as a dialog over a dimmed page: a page lies around it on
    /// all four sides, and its sides are edges or no window overlaps it partly. When it is the bright body of a window the
    /// rectangle method found, under a dark title bar, that whole window is the Subject instead.
    /// </para>
    /// <para>
    /// Otherwise the Subject is the topmost window: from the largest, the largest window lying wholly on top of it in
    /// turn, so a dialog opened from another dialog wins over it. A menu that sticks out past its window is not on top of
    /// it in this sense, so the window wins and the menu is kept as a Popup overhang. In a browser screenshot with no
    /// dialog, the Subject is the web page below the Browser bar.
    /// </para>
    /// <para>
    /// When no Subject is found, the image is already cropped when its own edges are the sides of its Subject: the best
    /// window is the whole image within the corner margin, or each of the image's four corners is cut off, as a rounded
    /// corner or a sliver of page left along the sides shows. A featureless image, or a page that reaches the image's
    /// corners, is nothing found.
    /// </para>
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static Finding Find(RgbaImage image, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var edges = new EdgeMap(image, RectangleMethod.MinimumRun, cancellation);
        var rectangles = RectangleMethod.FindRectangles(edges, cancellation);
        var window = ChooseWindow(image, edges, rectangles, cancellation);
        if (window is { } found && !IsWholeImage(found, image))
            return Finding.Of(new Subject(found) { Popups = RectangleMethod.Popups(image, edges, rectangles, found) });
        if (BrowserBar.FindPage(image, cancellation) is { } page)
            return Finding.Of(new Subject(page));
        return window is not null || HasCutCorners(image) ? Finding.AlreadyCropped : Finding.NothingFound;
    }

    /// <summary>
    /// The largest Subject fully inside <paramref name="region"/>, such as a button, a menu, a card or a tile, or
    /// <c>null</c> when none is found. The part of the region outside the image is ignored.
    /// </summary>
    /// <remarks>
    /// The Subject itself wins over a larger frame just around it, such as the one the edges of neighbouring controls
    /// form around a button. Nothing inside a region has to cast a shadow.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public static Subject? FindInRegion(RgbaImage image, PixelRect region, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        return RegionMethod.Find(image, region, cancellation) is { } found ? new Subject(found) : null;
    }

    /// <summary>The pixels at each end of a side that may be lost to a rounded corner.</summary>
    private const int CornerMargin = RectangleMethod.CornerMargin;

    private static PixelRect? ChooseWindow(
        RgbaImage image, EdgeMap edges, IReadOnlyList<PixelRect> rectangles, CancellationToken cancellation)
    {
        var windows = RectangleMethod.Windows(image, rectangles);
        // The bright area can hold a popup attached to the dialog: of its rectangles, the dialog is the largest the
        // rectangle method also found, as it does not find the popup's columns stretched over the dialog.
        var bright = BrightnessMethod.FindWindows(image, cancellation);
        var assembled = bright.Where(candidate => rectangles.Any(rectangle => RectangleMethod.Competes(rectangle, candidate)));
        if (assembled.Concat(bright).FirstOrDefault() is not { IsEmpty: false } dialog
            || !IsDimmedDialog(dialog, image, edges, rectangles, windows))
            return Topmost(windows);
        // A light window body under a dark title bar is bright too, with a wallpaper around it that is not: the window
        // the rectangle method finds around it, sharing its other sides, is the whole window.
        foreach (var window in windows)
            if (IsBodyOf(dialog, window))
                return window;
        return dialog;
    }

    /// <summary>
    /// The topmost of <paramref name="windows"/> (largest first): from the largest, the largest window lying wholly on top
    /// of it in turn, as a dialog opened from another dialog does; <c>null</c> when there are none.
    /// </summary>
    private static PixelRect? Topmost(IReadOnlyList<PixelRect> windows)
    {
        if (windows.Count == 0)
            return null;
        var topmost = windows[0];
        while (windows.FirstOrDefault(window => window != topmost && topmost.Contains(window)) is { IsEmpty: false } inner)
            topmost = inner;
        return topmost;
    }

    /// <summary>
    /// Whether the brightness method's <paramref name="window"/> is a dialog over a dimmed page:
    /// <list type="bullet">
    /// <item>A page lies around it on all four sides. The bright body of an undimmed light page reaches the image's edge.</item>
    /// <item>Its sides are edges, or else no window overlaps it partly. The bright area of undimmed windows overlapping each
    /// other on a darker wallpaper ends where no edge is.</item>
    /// </list>
    /// </summary>
    private static bool IsDimmedDialog(
        PixelRect window, RgbaImage image, EdgeMap edges, IReadOnlyList<PixelRect> rectangles, IReadOnlyList<PixelRect> windows) =>
        IsSurroundedByPage(window, image)
        && (RectangleMethod.IsOutlined(edges, window, rectangles) || !windows.Any(other => OverlapsPartly(window, other)));

    private static bool OverlapsPartly(PixelRect a, PixelRect b) =>
        !a.Intersect(b).IsEmpty && !a.Contains(b) && !b.Contains(a);

    /// <summary>
    /// Whether a page lies around <paramref name="window"/> on all four sides. A bright area that reaches the image's
    /// edge is the body of an undimmed light page rather than a dialog over a dimmed one.
    /// </summary>
    private static bool IsSurroundedByPage(PixelRect window, RgbaImage image) =>
        window.X > 0 && window.Y > 0 && window.Right < image.Width && window.Bottom < image.Height;

    /// <summary>
    /// Whether <paramref name="body"/> is the bright body of <paramref name="window"/>: inside it, sharing at least two of
    /// its sides, and short of it by more than the corner margin on another.
    /// </summary>
    private static bool IsBodyOf(PixelRect body, PixelRect window)
    {
        if (!window.Contains(body))
            return false;
        int[] gaps = [body.X - window.X, body.Y - window.Y, window.Right - body.Right, window.Bottom - body.Bottom];
        return gaps.Count(gap => gap <= CornerMargin) >= 2 && gaps.Any(gap => gap > CornerMargin);
    }

    /// <summary>
    /// Whether all four corners of <paramref name="image"/> are cut off: each corner pixel differs by at least an edge's
    /// step from the pixel just past the corner margin diagonally inside it, as where a rounded corner or a sliver of
    /// page left along the sides shows outside a Subject cropped to its edges.
    /// </summary>
    private static bool HasCutCorners(RgbaImage image)
    {
        const int inset = CornerMargin + 1;
        if (image.Width <= 2 * inset || image.Height <= 2 * inset)
            return false;
        var (right, bottom) = (image.Width - 1, image.Height - 1);
        return IsCut(0, 0, inset, inset) && IsCut(right, 0, right - inset, inset)
            && IsCut(0, bottom, inset, bottom - inset) && IsCut(right, bottom, right - inset, bottom - inset);

        bool IsCut(int x, int y, int insideX, int insideY) =>
            image[x, y].DifferenceTo(image[insideX, insideY]) >= EdgeMap.EdgeStep;
    }

    private static bool IsWholeImage(PixelRect window, RgbaImage image) =>
        window.X <= CornerMargin && window.Y <= CornerMargin
        && window.Right >= image.Width - CornerMargin && window.Bottom >= image.Height - CornerMargin;
}
