using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>The render module's single entry point: Original + Steps → Result + report.</summary>
public static class Renderer
{
    /// <summary>
    /// Applies <paramref name="steps"/> to <paramref name="original"/> and returns the Result.
    /// The Original is never modified, so rendering again always starts from its pixels.
    /// </summary>
    public static RenderOutput Render(RgbaImage original, Steps steps)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(steps);

        var bounds = new PixelRect(0, 0, original.Width, original.Height);
        var (window, popups) = steps.Crop is null ? (bounds, []) : Clip(steps.Crop, bounds);
        var overhang = Overhang.Of(window, Union(window, popups));
        var report = new RenderReport { InputHasShadowPadding = AlreadyProcessed.HasShadowPadding(original) };

        // Frame lines narrow the window inside the Original, so the popups are kept whole whatever is removed.
        if (steps.FrameLines)
        {
            (window, var removed) = FrameLines.Trim(original, window, overhang);
            report = report with { FrameLinesRemoved = removed };
        }

        var result = Compose(original, window, popups);

        if (steps.RoundedCorners is not null)
        {
            var (measured, applied) = CornerRounding.Apply(result, steps.RoundedCorners, overhang);
            report = report with { MeasuredRadii = measured, AppliedRadii = applied };
        }

        if (steps.Shadow is not null)
        {
            (result, var padding) = ShadowDrawing.Apply(result, steps.Shadow);
            report = report with { ShadowPadding = padding };
        }

        return new RenderOutput(result, report);
    }

    /// <summary>The crop's window and popups clipped to the image, leaving out popups wholly outside it.</summary>
    private static (PixelRect Window, PixelRect[] Popups) Clip(Crop crop, PixelRect bounds)
    {
        var window = crop.Window.Intersect(bounds);
        if (window.IsEmpty)
            throw new ArgumentException($"The crop {crop.Window} lies entirely outside the {bounds.Width}x{bounds.Height} image.", nameof(crop));
        return (window, crop.Popups.Select(p => p.Intersect(bounds)).Where(p => !p.IsEmpty).ToArray());
    }

    private static PixelRect Union(PixelRect window, PixelRect[] popups) =>
        popups.Aggregate(window, (union, popup) => union.Union(popup));

    /// <summary>
    /// Copies the window and each popup into a fully transparent image of their bounding rectangle,
    /// so nothing beside them shows.
    /// </summary>
    private static RgbaImage Compose(RgbaImage source, PixelRect window, PixelRect[] popups)
    {
        var area = Union(window, popups);
        var result = new RgbaImage(area.Width, area.Height);
        foreach (var part in popups.Prepend(window))
            for (var y = part.Y; y < part.Bottom; y++)
                for (var x = part.X; x < part.Right; x++)
                    result[x - area.X, y - area.Y] = source[x, y];
        return result;
    }
}

/// <summary>The Result's pixels and what the render measured.</summary>
public sealed record RenderOutput(RgbaImage Result, RenderReport Report);
