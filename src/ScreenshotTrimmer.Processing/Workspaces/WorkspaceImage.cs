namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>One entry in a <see cref="Workspace"/>'s list.</summary>
public sealed record WorkspaceImage
{
    /// <summary>What identifies the entry in its list: its file's path, or a Pasted image's number until it is saved.</summary>
    public required ImageKey Key { get; init; }

    /// <summary>Full path of the image file; <c>null</c> for a Pasted image that has not been saved yet.</summary>
    public string? Path => Key.Path;

    /// <summary>
    /// For a Pasted image that has not been saved yet, its number in the order the images were pasted into this list,
    /// starting at 1, so it can be named for display ("Pasted image 1"); otherwise <c>null</c>.
    /// </summary>
    public int? PastedNumber => Key.PastedNumber;

    /// <summary>Whether this is a Pasted image that has not been saved yet, so it has no file.</summary>
    public bool IsPasted => PastedNumber is not null;

    /// <summary>Whether this image is the Suffix-mode Result of another listed image.</summary>
    public bool IsResult { get; init; }

    /// <summary>Whether this image's Suffix-mode Result exists beside it.</summary>
    public bool HasResult { get; init; }

    /// <summary>The file's content version when it was listed; it changes when the file is overwritten.</summary>
    public ContentVersion ContentVersion { get; init; }

    /// <summary>
    /// Whether the image's current Steps differ from the Steps at its last successful save in this session
    /// (<c>Steps.None</c> until then), so its Result on disk does not show them. Always set for a Pasted image that has
    /// not been saved yet.
    /// </summary>
    public bool HasUnsavedSteps { get; init; }

    /// <summary>
    /// Whether a Batch found no Subject in this image and so gave it no Steps, shown as the "No subject found" marker.
    /// Cleared when the image gets Steps, by hand or from a later Batch. Session-only: dropped with the image and when
    /// another list is opened.
    /// </summary>
    public bool NoSubjectFound { get; init; }
}
