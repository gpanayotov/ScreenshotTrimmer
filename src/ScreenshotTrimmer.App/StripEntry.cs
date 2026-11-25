using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>One image in the strip.</summary>
public sealed class StripEntry(WorkspaceImage image)
{
    public WorkspaceImage Image { get; } = image;

    public string Name => Path.GetFileName(Image.Path);

    /// <summary>Whether this image's Result exists beside it, shown as the "has result" badge.</summary>
    public bool HasResult => Image.HasResult;

    /// <summary>Listed Results, shown only when the user asks for them, are dimmed so the Originals stand out.</summary>
    public double Opacity => Image.IsResult ? 0.6 : 1.0;
}
