namespace ScreenshotTrimmer.Processing.Tests;

/// <summary>A fact that needs a case-sensitive file system, as on Linux: skipped on Windows and macOS.</summary>
internal sealed class LinuxFactAttribute : FactAttribute
{
    public LinuxFactAttribute()
    {
        if (!OperatingSystem.IsLinux())
            Skip = "Needs a case-sensitive file system.";
    }
}
