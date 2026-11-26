namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// What <see cref="SubjectDetection.Find"/> finds in a whole image: a <b>Subject</b> to crop to, that the image is
/// <b>Already cropped</b> to its Subject, or nothing.
/// </summary>
public sealed record Finding
{
    private Finding(Subject? subject, bool isAlreadyCropped)
    {
        Subject = subject;
        IsAlreadyCropped = isAlreadyCropped;
    }

    /// <summary>No Subject was found, and the image is not already cropped to one.</summary>
    public static Finding NothingFound { get; } = new(null, false);

    /// <summary>The image is already cropped to its Subject, so there is nothing to crop.</summary>
    public static Finding AlreadyCropped { get; } = new(null, true);

    /// <summary>A Subject to crop to was found.</summary>
    public static Finding Of(Subject subject) => new(subject ?? throw new ArgumentNullException(nameof(subject)), false);

    /// <summary>The Subject to crop to, or <c>null</c> when the image is already cropped or nothing was found.</summary>
    public Subject? Subject { get; }

    /// <summary>Whether the image is already cropped to its Subject.</summary>
    public bool IsAlreadyCropped { get; }
}
