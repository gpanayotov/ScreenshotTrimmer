using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>What the Batch tests share: the remembered settings, finding entries by name, and a progress they can act in.</summary>
internal static class BatchTesting
{
    /// <summary>Remembered finishing that differs from the defaults, so a Batch's Steps show where they came from.</summary>
    public static readonly UserSettings Remembered = new()
    {
        FrameLines = true, RoundedCorners = false, ShadowOn = true, ShadowSize = 1.5, ShadowOpacity = 0.5,
    };

    /// <summary>The listed entry named <paramref name="name"/>.</summary>
    public static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(image => Path.GetFileName(image.Path) == name);

    /// <summary>The file names of <paramref name="images"/>, in order.</summary>
    public static string[] Names(IEnumerable<WorkspaceImage> images) =>
        images.Select(image => Path.GetFileName(image.Path)!).ToArray();
}

/// <summary>
/// An <see cref="IProgress{T}"/> that reports on the Batch's own thread, before it goes on, so a test can act at a fixed
/// point of a Batch and the reports are in order when the run returns.
/// </summary>
internal sealed class SynchronousProgress(Action<BatchProgress> report) : IProgress<BatchProgress>
{
    public void Report(BatchProgress value) => report(value);
}
