using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>How saving one image went during <see cref="Workspace.SaveAll"/>.</summary>
public sealed record SaveOutcome
{
    /// <summary>The entry as it was listed when it was saved; a Backup-mode save may since have renamed it.</summary>
    public required WorkspaceImage Image { get; init; }

    /// <summary>What the save changed on disk, or <c>null</c> when it failed or was not attempted.</summary>
    public SaveReport? Report { get; init; }

    /// <summary>
    /// Why the save failed, or <c>null</c> when it succeeded or was not attempted: a <see cref="SaveFailedException"/> when the Result could
    /// not be written, or the <see cref="IOException"/> or <see cref="InvalidDataException"/> from reading the
    /// Original. The image's Steps stay unsaved.
    /// </summary>
    public Exception? Failure { get; init; }

    /// <summary>
    /// Whether the image is a Pasted image that has not been saved yet, so it was not saved: it has no file until the
    /// user chooses where to save it.
    /// </summary>
    public bool NeedsLocation { get; init; }
}
