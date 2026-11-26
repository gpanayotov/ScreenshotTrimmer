namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>
/// Where the local samples check finds the maintainer's git-ignored real screenshots and writes what it derives from
/// them: <c>samples/</c> and <c>samples-overview/</c> at the root of the repository the tests run from.
/// </summary>
internal static class SamplesFolder
{
    /// <summary>The repository root: the nearest folder above the test assembly that holds the solution file.</summary>
    public static string? RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>The samples folder, or <c>null</c> when the repository has none.</summary>
    public static string? Samples { get; } =
        RepositoryRoot is { } root && Directory.Exists(Path.Combine(root, "samples")) ? Path.Combine(root, "samples") : null;

    /// <summary>The folder for the overview pictures and the report.</summary>
    public static string Overview => Path.Combine(RepositoryRoot!, "samples-overview");

    private static string? FindRepositoryRoot()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
            if (File.Exists(Path.Combine(folder.FullName, "ScreenshotTrimmer.slnx")))
                return folder.FullName;
        return null;
    }
}
