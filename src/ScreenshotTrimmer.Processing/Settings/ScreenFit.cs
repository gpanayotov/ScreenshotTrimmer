using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>Keeps a restored window where the user can see and grab it.</summary>
public static class ScreenFit
{
    /// <summary>How much of the top of the window counts as its title bar, in physical pixels.</summary>
    private const int TitleBarHeight = 30;

    /// <summary>How much of the title bar's width must be on a screen for the window to be grabbed, in physical pixels.</summary>
    private const int GrabWidth = 100;

    /// <summary>
    /// Where to put <paramref name="window"/>, given each screen's <paramref name="workingAreas"/>, all in physical
    /// screen pixels. A window whose title bar can be grabbed on some screen stays as it is; any other moves onto the
    /// screen nearest to it, shrinking to fit that screen. With no screens known, the window stays as it is.
    /// </summary>
    public static PixelRect Place(PixelRect window, IReadOnlyList<PixelRect> workingAreas)
    {
        ArgumentNullException.ThrowIfNull(workingAreas);
        if (workingAreas.Count == 0 || workingAreas.Any(area => CanGrab(window, area)))
            return window;

        var target = workingAreas.MinBy(area => DistanceSquared(Centre(window), area));
        var width = Math.Min(window.Width, target.Width);
        var height = Math.Min(window.Height, target.Height);
        return new PixelRect(
            Math.Clamp(window.X, target.X, target.Right - width),
            Math.Clamp(window.Y, target.Y, target.Bottom - height),
            width,
            height);
    }

    private static bool CanGrab(PixelRect window, PixelRect area)
    {
        var titleBar = window with { Height = Math.Min(window.Height, TitleBarHeight) };
        var visible = titleBar.Intersect(area);
        return visible.Width >= Math.Min(window.Width, GrabWidth) && visible.Height >= titleBar.Height;
    }

    private static (long X, long Y) Centre(PixelRect rect) => (rect.X + rect.Width / 2L, rect.Y + rect.Height / 2L);

    /// <summary>The squared distance from <paramref name="point"/> to the nearest point of <paramref name="area"/>.</summary>
    private static long DistanceSquared((long X, long Y) point, PixelRect area)
    {
        var dx = Math.Max(Math.Max(area.X - point.X, point.X - area.Right), 0);
        var dy = Math.Max(Math.Max(area.Y - point.Y, point.Y - area.Bottom), 0);
        return dx * dx + dy * dy;
    }
}
