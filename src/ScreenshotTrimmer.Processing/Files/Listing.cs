namespace ScreenshotTrimmer.Processing.Files;

/// <summary>The images to show for the paths the user opened, in display order.</summary>
public sealed record Listing
{
    public required IReadOnlyList<ListedImage> Images { get; init; }

    /// <summary>The image to show first: the file the user opened on its own, otherwise <c>null</c>.</summary>
    public ListedImage? Selected { get; init; }
}

/// <summary>One image in a <see cref="Listing"/>.</summary>
public sealed record ListedImage
{
    /// <summary>Full path of the image file.</summary>
    public required string Path { get; init; }

    /// <summary>Whether this image is the Suffix-mode Result of another listed image (only listed when asked for).</summary>
    public bool IsResult { get; init; }

    /// <summary>Whether this image's Suffix-mode Result exists beside it, whether or not the Result is listed.</summary>
    public bool HasResult { get; init; }
}
