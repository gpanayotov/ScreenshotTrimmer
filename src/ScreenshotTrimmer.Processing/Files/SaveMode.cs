namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Where a Result goes when it is saved next to its Original.</summary>
public enum SaveMode
{
    /// <summary>The Original is left untouched; the Result is saved under the Original's name plus a suffix.</summary>
    Suffix,

    /// <summary>The Result takes the Original's name; the Original is kept under its name plus a suffix.</summary>
    Backup,
}
