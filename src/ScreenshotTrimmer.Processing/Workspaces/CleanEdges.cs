using System.Globalization;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// What the <b>Clean edges</b> controls show for one image: its own Steps' choices, or the remembered settings while
/// it has no Steps, so the controls always show what the image's next Step would start from.
/// </summary>
/// <param name="FrameLines">Whether frame lines are removed.</param>
/// <param name="RoundedCorners">Whether the pixels outside rounded corners become transparent.</param>
/// <param name="ManualRadius">The image's manual corner radius in pixels, or null for the measured radii.</param>
public sealed record CleanEdges(bool FrameLines, bool RoundedCorners, double? ManualRadius)
{
    /// <summary>The largest manual radius accepted, in pixels; a radius never rounds past half the Result's side anyway.</summary>
    public const double MaxManualRadius = 1000;

    /// <summary>The Clean edges choices <paramref name="steps"/> hold, or those in <paramref name="remembered"/> when there are none.</summary>
    public static CleanEdges Shown(Steps steps, UserSettings remembered)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(remembered);

        return steps == Steps.None
            ? new CleanEdges(remembered.FrameLines, remembered.RoundedCorners, null)
            : new CleanEdges(steps.FrameLines, steps.RoundedCorners is not null, steps.RoundedCorners?.ManualRadius);
    }

    /// <summary>The change that turns frame-line removal on or off, for <see cref="FirstStep.Apply"/>.</summary>
    public static Func<Steps, Steps> SetFrameLines(bool on) => steps => steps with { FrameLines = on };

    /// <summary>
    /// The change that turns rounded corners on or off, for <see cref="FirstStep.Apply"/>. Turning them on when they
    /// are already on keeps the manual radius.
    /// </summary>
    public static Func<Steps, Steps> SetRoundedCorners(bool on) =>
        steps => steps with { RoundedCorners = on ? steps.RoundedCorners ?? new RoundedCorners() : null };

    /// <summary>
    /// The change that sets the manual corner radius, or with null returns to the measured radii, for
    /// <see cref="FirstStep.Apply"/>. It changes nothing while rounded corners are off.
    /// </summary>
    public static Func<Steps, Steps> SetManualRadius(double? radius) =>
        steps => steps.RoundedCorners is { } corners
            ? steps with { RoundedCorners = corners with { ManualRadius = radius } }
            : steps;

    /// <summary>
    /// Reads a manual radius typed in the interface language: empty means automatic (null), otherwise a number of
    /// pixels from 0 to <see cref="MaxManualRadius"/>.
    /// </summary>
    /// <returns>Whether <paramref name="text"/> was a radius.</returns>
    public static bool TryParseManualRadius(string? text, IFormatProvider culture, out double? radius)
    {
        radius = null;
        if (string.IsNullOrWhiteSpace(text))
            return true;
        if (!double.TryParse(text.Trim(), NumberStyles.Float, culture, out var value)
            || !double.IsFinite(value) || value < 0 || value > MaxManualRadius)
            return false;
        radius = value;
        return true;
    }
}
