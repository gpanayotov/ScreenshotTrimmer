using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// The images the user opened, in strip order, and which one is selected. Has no interface dependency, so the app's
/// strip and a command-line tool can share it.
/// </summary>
public sealed class Workspace : IDisposable
{
    private readonly UserSettings _settings;

    public Workspace(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
    }

    /// <summary>The listed images in strip order.</summary>
    public IReadOnlyList<WorkspaceImage> Images { get; private set; } = [];

    /// <summary>The selected image, or <c>null</c> when nothing is listed.</summary>
    public WorkspaceImage? Selected { get; private set; }

    /// <summary>Raised once per update to the list or the selection, on the thread that made it.</summary>
    public event EventHandler<WorkspaceChange>? Changed;

    /// <summary>
    /// Replaces the list with the images for <paramref name="paths"/>: one file lists its whole folder with that file
    /// selected; a folder or several paths list their images with the first selected. Paths that exist neither as a
    /// file nor as a folder are skipped and returned, so the caller can tell the user.
    /// </summary>
    public IReadOnlyList<string> Open(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var fullPaths = paths.Select(Path.GetFullPath).ToList();
        var missing = fullPaths.Where(path => !File.Exists(path) && !Directory.Exists(path)).ToList();
        var existing = fullPaths.Except(missing).ToList();

        var listing = ImageListing.List(existing, ListingOptionsFor(_settings));
        Images = listing.Images
            .Select(image => new WorkspaceImage
            {
                Path = image.Path, IsResult = image.IsResult, HasResult = image.HasResult,
            })
            .ToList();
        Selected = listing.Selected is { } opened
            ? Images.First(image => image.Path == opened.Path)
            : Images.FirstOrDefault();

        RaiseChanged();
        return missing;
    }

    public void Dispose()
    {
    }

    private void RaiseChanged() =>
        Changed?.Invoke(this, new WorkspaceChange { Images = Images, Selected = Selected });

    private static ListingOptions ListingOptionsFor(UserSettings settings) =>
        ListingOptions.For(new SaveOptions
        {
            Mode = settings.SaveMode,
            ResultSuffix = settings.ResultSuffix,
            BackupSuffix = settings.BackupSuffix,
        });
}
