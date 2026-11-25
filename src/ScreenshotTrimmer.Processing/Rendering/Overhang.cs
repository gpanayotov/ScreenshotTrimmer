using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The sides of the window that a <b>Popup overhang</b> sticks out past. Clean edges leave these sides
/// alone: no frame lines are removed there and the corners on them stay square.
/// </summary>
internal readonly record struct Overhang(bool Left, bool Top, bool Right, bool Bottom)
{
    /// <summary>The sides on which <paramref name="area"/>, the whole crop, reaches past <paramref name="window"/>.</summary>
    public static Overhang Of(PixelRect window, PixelRect area) =>
        new(area.X < window.X, area.Y < window.Y, area.Right > window.Right, area.Bottom > window.Bottom);
}
