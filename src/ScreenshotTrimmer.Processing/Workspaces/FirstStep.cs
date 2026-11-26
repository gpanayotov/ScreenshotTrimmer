using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// The rule that an image's first <b>Step</b> takes its finishing from the remembered settings, so drawing a crop gives
/// the finished look straight away. Later Steps change only what the user asked for.
/// </summary>
public static class FirstStep
{
    /// <summary>
    /// The Steps after <paramref name="change"/>: applied to <paramref name="current"/>, or, when the image has no
    /// Steps yet, to the finishing in <paramref name="remembered"/> (frame lines, rounded corners, and the Shadow with
    /// its size and opacity). The Shadow is left off when <paramref name="originalHasShadowPadding"/>, so an image that
    /// was already processed never gets a second one.
    /// </summary>
    public static Steps Apply(
        Steps current, UserSettings remembered, bool originalHasShadowPadding, Func<Steps, Steps> change)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(change);

        if (current != Steps.None)
            return change(current);
        return change(new Steps
        {
            FrameLines = remembered.FrameLines,
            RoundedCorners = remembered.RoundedCorners ? new RoundedCorners() : null,
            Shadow = remembered.Shadow && !originalHasShadowPadding
                ? new Shadow { Size = remembered.ShadowSize, Opacity = remembered.ShadowOpacity }
                : null,
        });
    }
}
