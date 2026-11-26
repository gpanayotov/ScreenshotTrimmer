using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>Why a <b>Batch</b> leaves a listed image alone.</summary>
public enum BatchSkip
{
    /// <summary>The image has Steps, saved or not, so the Batch would overwrite hand work.</summary>
    HasSteps,

    /// <summary>The image already has a Shadow's transparent padding (<see cref="AlreadyProcessed.HasShadowPadding"/>).</summary>
    AlreadyProcessed,

    /// <summary>The image is the Result of another listed image.</summary>
    Result,

    /// <summary>The image left the list or the disk while the Batch ran.</summary>
    Gone,
}

/// <summary>Decides which listed images a <b>Batch</b> processes, so it never redoes finished work or overwrites hand work.</summary>
public static class BatchSelection
{
    /// <summary>
    /// Why a Batch skips <paramref name="image"/>, or <c>null</c> when it processes it: a Result, an image that
    /// <paramref name="hasSteps"/>, or one already processed, checked in that order. A Pasted image with no Steps is
    /// processed. <paramref name="loadOriginal"/> is called only for an image not already ruled out by the cheaper checks.
    /// </summary>
    public static BatchSkip? SkipReason(WorkspaceImage image, bool hasSteps, Func<RgbaImage> loadOriginal)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(loadOriginal);

        if (image.IsResult)
            return BatchSkip.Result;
        if (hasSteps)
            return BatchSkip.HasSteps;
        return AlreadyProcessed.HasShadowPadding(loadOriginal()) ? BatchSkip.AlreadyProcessed : null;
    }
}
