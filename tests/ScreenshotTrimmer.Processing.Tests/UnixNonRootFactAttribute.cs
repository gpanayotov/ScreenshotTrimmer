namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>
/// A fact that needs Unix file permissions to be enforced: skipped on Windows, where folder modes do not
/// stop writes, and for root, who ignores them.
/// </summary>
internal sealed class UnixNonRootFactAttribute : FactAttribute
{
    public UnixNonRootFactAttribute()
    {
        if (OperatingSystem.IsWindows())
            Skip = "Unix file permissions are not available on Windows.";
        else if (Environment.UserName == "root")
            Skip = "root ignores file permissions.";
    }
}
