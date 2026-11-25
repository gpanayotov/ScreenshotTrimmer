namespace ScreenshotTrimmer.Processing.Files;

/// <summary>How <see cref="ResultFiles.Save"/> names and places a Result.</summary>
public sealed record SaveOptions
{
    /// <summary>The save rule to apply.</summary>
    public SaveMode Mode { get; init; } = SaveMode.Suffix;

    /// <summary>Appended to the Original's name for the Result in Suffix mode.</summary>
    public string ResultSuffix { get; init; } = "-trimmed";

    /// <summary>Appended to the Original's name for the backup in Backup mode.</summary>
    public string BackupSuffix { get; init; } = "-original";
}
