namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>One entry in a <see cref="Workspace"/>'s list.</summary>
public sealed record WorkspaceImage
{
    /// <summary>Full path of the image file.</summary>
    public required string Path { get; init; }

    /// <summary>Whether this image is the Suffix-mode Result of another listed image.</summary>
    public bool IsResult { get; init; }

    /// <summary>Whether this image's Suffix-mode Result exists beside it.</summary>
    public bool HasResult { get; init; }

    /// <summary>The file's content version when it was listed; it changes when the file is overwritten.</summary>
    public ContentVersion ContentVersion { get; init; }
}
