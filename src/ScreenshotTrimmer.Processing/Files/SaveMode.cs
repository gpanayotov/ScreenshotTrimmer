namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Which save rule <see cref="ResultFiles.Save"/> applies to a Result of an Original.</summary>
public enum SaveMode
{
    /// <summary>The Original is left untouched; the Result is saved beside it under its name plus the Result suffix.</summary>
    Suffix,

    /// <summary>The Original is kept under its name plus the backup suffix; the Result takes the Original's name.</summary>
    Backup,
}
