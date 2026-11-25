namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// The one rule for a suffix appended to a file's base name (the Result suffix and the backup suffix):
/// not empty, and free of characters a file name may not hold on Windows, Linux or macOS.
/// Settings loading and saving and every Result save check suffixes here.
/// </summary>
public static class FileSuffix
{
    private static readonly char[] Forbidden = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>Whether <paramref name="suffix"/> can be appended to a file name.</summary>
    public static bool IsValid(string? suffix) =>
        !string.IsNullOrEmpty(suffix)
        && suffix.IndexOfAny(Forbidden) < 0
        && !suffix.Any(char.IsControl);

    /// <exception cref="ArgumentException"><paramref name="suffix"/> is not <see cref="IsValid">valid</see>.</exception>
    public static void Require(string? suffix, string paramName)
    {
        if (!IsValid(suffix))
            throw new ArgumentException(
                $"'{suffix}' is not a valid file suffix: it must not be empty or contain control characters or any of {string.Join(' ', Forbidden)}.",
                paramName);
    }
}
