namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// How file names compare on this operating system: ignoring letter case on Windows and macOS, whose file
/// systems do by default, and exactly on Linux, where <c>Shot.png</c> and <c>shot.png</c> are different files.
/// </summary>
internal static class FileNames
{
    public static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static readonly StringComparer Comparer = StringComparer.FromComparison(Comparison);

    public static bool Same(string a, string b) => string.Equals(a, b, Comparison);
}
