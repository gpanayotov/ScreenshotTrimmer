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

        var result = steps.Crop is null ? original.Clone() : ApplyCrop(original, steps.Crop);
        var report = new RenderReport();

        if (steps.FrameLines)
        {
            (result, var removed) = FrameLines.Remove(result);
            report = report with { FrameLinesRemoved = removed };
        }

        if (steps.RoundedCorners is not null)
        {
            var (measured, applied) = CornerRounding.Apply(result, steps.RoundedCorners);
            report = report with { MeasuredRadii = measured, AppliedRadii = applied };
        }

        if (steps.Shadow is not null)
        {
            (result, var padding) = ShadowDrawing.Apply(result, steps.Shadow);
            report = report with { ShadowPadding = padding };
        }

        return new RenderOutput(result, report);
    }

    private static RgbaImage ApplyCrop(RgbaImage source, Crop crop)
    {
        var bounds = new PixelRect(0, 0, source.Width, source.Height);
        var window = crop.Window.Intersect(bounds);
        if (window.IsEmpty)
            throw new ArgumentException($"The crop {crop.Window} lies entirely outside the {source.Width}x{source.Height} image.", nameof(crop));

        var popups = crop.Popups.Select(p => p.Intersect(bounds)).Where(p => !p.IsEmpty).ToArray();
        var area = popups.Aggregate(window, (union, popup) => union.Union(popup));

        // Start fully transparent and copy in the window and each popup, so nothing beside them shows.
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
