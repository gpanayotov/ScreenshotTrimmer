namespace ScreenshotTrimmer.Processing.Files;

/// <summary>How <see cref="ImageListing.List"/> recognises and treats Results.</summary>
public sealed record ListingOptions
{
    /// <summary>
    /// The Suffix-mode suffix: a file whose base name is another listed image's base name plus this suffix is a
    /// Result. <c>null</c> (or empty) when the save mode is not Suffix mode, so no file is treated as a Result.
    /// </summary>
    public string? ResultSuffix { get; init; } = "-trimmed";

    /// <summary>Whether Results are listed (marked <see cref="ListedImage.IsResult"/>) instead of hidden.</summary>
    public bool IncludeResults { get; init; }

    /// <summary>The listing options that match <paramref name="save"/>: Results are recognised only in Suffix mode.</summary>
    public static ListingOptions For(SaveOptions save, bool includeResults = false)
    {
        ArgumentNullException.ThrowIfNull(save);
        return new ListingOptions
        {
            ResultSuffix = save.Mode == SaveMode.Suffix ? save.ResultSuffix : null,
            IncludeResults = includeResults,
        };
    }
}
