using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Settings;

/// <summary>The interface language: follow the operating system, English or Bulgarian.</summary>
public enum Language
{
    System,
    En,
    Bg,
}

/// <summary>The user's settings remembered between sessions. A new instance holds the defaults.</summary>
public sealed record UserSettings
{
    public SaveMode SaveMode { get; init; } = SaveMode.Suffix;

    /// <summary>Appended to the Original's name for the Result in Suffix mode.</summary>
    public string ResultSuffix { get; init; } = SaveOptions.DefaultResultSuffix;

    /// <summary>Appended to the Original's name for the kept Original in Backup mode.</summary>
    public string BackupSuffix { get; init; } = SaveOptions.DefaultBackupSuffix;

    /// <summary>Scales the Shadow's blur and padding; 1.0 is the default look.</summary>
    public double ShadowSize { get; init; } = Shadow.DefaultSize;

    /// <summary>The Shadow's opacity, from 0 to 1.</summary>
    public double ShadowOpacity { get; init; } = Shadow.DefaultOpacity;

    /// <summary>Whether Clean edges removes frame lines.</summary>
    public bool FrameLines { get; init; } = true;

    /// <summary>Whether Clean edges makes the pixels outside rounded corners transparent.</summary>
    public bool RoundedCorners { get; init; } = true;

    public Language Language { get; init; } = Language.System;
}
