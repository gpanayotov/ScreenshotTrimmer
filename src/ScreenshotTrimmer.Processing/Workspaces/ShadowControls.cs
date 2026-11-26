using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// What the <b>Shadow</b> controls show for one image: the switch, size and opacity of its own Steps, or of the
/// remembered settings while it has no Steps, so the controls always show what the image's next Step would start from.
/// The sliders keep the remembered size and opacity while the Shadow is off, ready for turning it on.
/// </summary>
/// <param name="On">Whether the Result gets a Shadow.</param>
/// <param name="Size">The Shadow's size; see <see cref="Shadow.IsAllowedSize"/>.</param>
/// <param name="Opacity">The Shadow's opacity; see <see cref="Shadow.IsAllowedOpacity"/>.</param>
public sealed record ShadowControls(bool On, double Size, double Opacity)
{
    /// <summary>Whether the Result gets a Shadow.</summary>
    public bool On { get; init; } = On;

    /// <summary>The Shadow's size, always an allowed one.</summary>
    public double Size { get; init; } = Shadow.IsAllowedSize(Size)
        ? Size
        : throw new ArgumentOutOfRangeException(nameof(Size), Size, "The Shadow's size is outside the allowed range.");

    /// <summary>The Shadow's opacity, always an allowed one.</summary>
    public double Opacity { get; init; } = Shadow.IsAllowedOpacity(Opacity)
        ? Opacity
        : throw new ArgumentOutOfRangeException(nameof(Opacity), Opacity, "The Shadow's opacity is outside the allowed range.");

    /// <summary>
    /// The Shadow controls for <paramref name="steps"/>, or for <paramref name="remembered"/> when there are none. An
    /// image with no Steps whose Original already has shadow padding shows the Shadow off, as its seeded Steps will
    /// leave it off (<see cref="FirstStep"/>).
    /// </summary>
    public static ShadowControls Shown(Steps steps, UserSettings remembered, bool originalHasShadowPadding)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(remembered);

        if (steps.Shadow is { } shadow)
            return new ShadowControls(true, shadow.Size, shadow.Opacity);
        var on = steps == Steps.None && remembered.ShadowOn && !originalHasShadowPadding;
        return new ShadowControls(on, remembered.ShadowSize, remembered.ShadowOpacity);
    }

    /// <summary>The change that gives the Steps this Shadow, or none when off, for <see cref="FirstStep.Apply"/>.</summary>
    public Steps Change(Steps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return steps with { Shadow = On ? new Shadow { Size = Size, Opacity = Opacity } : null };
    }

    /// <summary><paramref name="settings"/> with these controls as the remembered Shadow switch, size and opacity.</summary>
    public UserSettings Remember(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with { ShadowOn = On, ShadowSize = Size, ShadowOpacity = Opacity };
    }
}
