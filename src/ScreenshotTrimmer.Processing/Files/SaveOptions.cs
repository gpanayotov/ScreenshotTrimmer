namespace ScreenshotTrimmer.Processing.Files;

/// <summary>How <see cref="ResultFiles.Save"/> names and places a Result.</summary>
public sealed record SaveOptions
{
    /// <summary>Appended to the Original's name for the Result in Suffix mode.</summary>
    public string ResultSuffix { get; init; } = "-trimmed";
}
