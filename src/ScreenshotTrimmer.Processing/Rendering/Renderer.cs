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
        var report = new RenderReport { InputHasShadowPadding = AlreadyProcessed.HasShadowPadding(original) };

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
        var area = crop.Window.Intersect(new PixelRect(0, 0, source.Width, source.Height));
        if (area.IsEmpty)
            throw new ArgumentException($"The crop {crop.Window} lies entirely outside the {source.Width}x{source.Height} image.", nameof(crop));

        var result = new RgbaImage(area.Width, area.Height);
        for (var y = 0; y < area.Height; y++)
            for (var x = 0; x < area.Width; x++)
                result[x, y] = source[area.X + x, area.Y + y];
        return result;
    }
}

/// <summary>The Result's pixels and what the render measured.</summary>
public sealed record RenderOutput(RgbaImage Result, RenderReport Report);
