namespace ScreenshotTrimmer.Processing.Files;

/// <summary>What a save changed on disk, so the interface can update its list and badges.</summary>
public sealed record SaveReport
{
    /// <summary>Full paths of the files written.</summary>
    public required IReadOnlyList<string> Written { get; init; }

    /// <summary>Files moved: in Backup mode, the Original moved to its backup name.</summary>
    public IReadOnlyList<FileMove> Moved { get; init; } = [];

    /// <summary>
    /// In Backup mode, the full path of a backup that already existed, so neither it nor the Original
    /// was moved; otherwise <c>null</c>.
    /// </summary>
    public string? ExistingBackup { get; init; }
}
