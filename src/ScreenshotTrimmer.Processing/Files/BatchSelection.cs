using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Decides which listed images a <b>Batch</b> processes, so it never redoes finished work or overwrites hand work.</summary>
public static class BatchSelection
{
    /// <summary>
    /// Returns, in listing order, the <paramref name="images"/> that have no pending Steps, are not Results and are
    /// not already processed (<see cref="AlreadyProcessed.HasShadowPadding"/>, the render report's check).
    /// <paramref name="loadOriginal"/> is called only for images not already ruled out by the cheaper checks.
    /// </summary>
    public static IReadOnlyList<ListedImage> Select(
        IEnumerable<ListedImage> images,
        Func<ListedImage, bool> hasPendingSteps,
        Func<ListedImage, RgbaImage> loadOriginal)
    {
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(hasPendingSteps);
        ArgumentNullException.ThrowIfNull(loadOriginal);

        return images
            .Where(image => !image.IsResult && !hasPendingSteps(image))
            .Where(image => !AlreadyProcessed.HasShadowPadding(loadOriginal(image)))
            .ToList();
    }
}
