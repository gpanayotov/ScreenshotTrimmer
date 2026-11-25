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
                ContentVersion = ContentVersion.Of(image.Path),
            })
            .ToList();
        Selected = listing.Selected is { } opened
            ? Images.First(image => image.Path == opened.Path)
            : Images.FirstOrDefault();

        RaiseChanged();
        return missing;
    }

    /// <summary>Selects <paramref name="image"/>, which must be one of <see cref="Images"/>.</summary>
    public void Select(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var index = IndexOf(image);
        if (index < 0)
            throw new ArgumentException("The image is not listed in this workspace.", nameof(image));
        SelectAt(index);
    }

    /// <summary>Selects the next image; does nothing on the last one.</summary>
    public void Next() => SelectAt(SelectedIndex + 1);

    /// <summary>Selects the previous image; does nothing on the first one.</summary>
    public void Previous() => SelectAt(SelectedIndex - 1);

    public void Dispose()
    {
    }

    private int SelectedIndex => Selected is null ? -1 : IndexOf(Selected);

    private int IndexOf(WorkspaceImage image)
    {
        for (var i = 0; i < Images.Count; i++)
        {
            if (Images[i].Path == image.Path)
                return i;
        }
        return -1;
    }

    /// <summary>Selects the image at <paramref name="index"/>; an index outside the list, or the selection itself, changes nothing.</summary>
    private void SelectAt(int index)
    {
        if (index < 0 || index >= Images.Count || index == SelectedIndex)
            return;
        Selected = Images[index];
        RaiseChanged();
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
