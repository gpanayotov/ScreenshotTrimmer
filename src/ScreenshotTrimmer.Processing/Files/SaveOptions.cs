namespace ScreenshotTrimmer.Processing.Files;

/// <summary>How <see cref="ResultFiles.Save"/> names and places a Result.</summary>
public sealed record SaveOptions
{
    /// <summary>The default <see cref="ResultSuffix"/>.</summary>
    public const string DefaultResultSuffix = "-trimmed";

    /// <summary>The default <see cref="BackupSuffix"/>.</summary>
    public const string DefaultBackupSuffix = "-original";

    /// <summary>The save rule to apply.</summary>
    public SaveMode Mode { get; init; } = SaveMode.Suffix;

    /// <summary>Appended to the Original's name for the Result in Suffix mode. Must be a valid <see cref="FileSuffix"/>.</summary>
    public string ResultSuffix { get; init; } = DefaultResultSuffix;

    /// <summary>Appended to the Original's name for the backup in Backup mode. Must be a valid <see cref="FileSuffix"/>.</summary>
    public string BackupSuffix { get; init; } = DefaultBackupSuffix;
}
