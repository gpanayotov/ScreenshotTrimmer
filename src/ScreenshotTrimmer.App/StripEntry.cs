using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>One image in the strip.</summary>
public sealed class StripEntry(WorkspaceImage image)
{
    public WorkspaceImage Image { get; } = image;

    public string Name => Path.GetFileName(Image.Path);
}
