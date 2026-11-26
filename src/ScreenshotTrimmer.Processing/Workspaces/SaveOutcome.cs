using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>How saving one image went during <see cref="Workspace.SaveAll"/>.</summary>
public sealed record SaveOutcome
{
    /// <summary>The entry as it was listed when it was saved; a Backup-mode save may since have renamed it.</summary>
    public required WorkspaceImage Image { get; init; }

    /// <summary>What the save changed on disk, or <c>null</c> when it failed.</summary>
    public SaveReport? Report { get; init; }

    /// <summary>
    /// Why the save failed, or <c>null</c> when it succeeded: a <see cref="SaveFailedException"/> when the Result could
    /// not be written, or the <see cref="IOException"/> or <see cref="InvalidDataException"/> from reading the
    /// Original. The image's Steps stay unsaved.
    /// </summary>
    public Exception? Failure { get; init; }
}
