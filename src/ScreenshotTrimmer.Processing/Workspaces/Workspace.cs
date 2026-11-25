using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// The images the user opened, in strip order, and which one is selected. Has no interface dependency, so the app's
/// strip and a command-line tool can share it.
/// </summary>
public sealed class Workspace : IDisposable
{
    private UserSettings _settings;
    private IReadOnlyList<string> _openedPaths = [];
    private bool _showResults;

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

        _openedPaths = existing;
        var listing = List();
        Selected = listing.Selected is { } opened
            ? Images.First(image => image.Path == opened.Path)
            : Images.FirstOrDefault();

        RaiseChanged();
        return missing;
    }

    /// <summary>
    /// Whether Suffix-mode Results are listed in their sorted place instead of hidden. Session-only and off by default;
    /// changing it keeps the selection, except that a selected Result being hidden gives way to its Original.
    /// </summary>
    public bool ShowResults
    {
        get => _showResults;
        set
        {
            if (value == _showResults)
                return;
            _showResults = value;
            var selected = Selected;
            List();
            Selected = Images.FirstOrDefault(image => image.Path == selected?.Path)
                ?? (selected is { IsResult: true } ? OriginalOf(selected) : null)
                ?? Images.FirstOrDefault();
            RaiseChanged();
        }
    }

    /// <summary>
    /// Lists the opened paths again with <paramref name="settings"/>' save mode and Result suffix, which decide which
    /// files are Results. The selection stays where the selected file is still listed; otherwise it passes to the
    /// entry now in its place.
    /// </summary>
    public void ApplySettings(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        if (_openedPaths.Count == 0)
            return;

        var selectedIndex = SelectedIndex;
        var selectedPath = Selected?.Path;
        List();
        Selected = Images.FirstOrDefault(image => image.Path == selectedPath)
            ?? (Images.Count == 0 ? null : Images[Math.Clamp(selectedIndex, 0, Images.Count - 1)]);
        RaiseChanged();
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

    /// <summary>The listed Original of the Result <paramref name="result"/>, if any.</summary>
    private WorkspaceImage? OriginalOf(WorkspaceImage result)
    {
        var stem = StemOf(result.Path);
        var suffix = _settings.ResultSuffix;
        if (!stem.EndsWith(suffix, FileNames.Comparison))
            return null;
        var originalStem = stem[..^suffix.Length];
        return Images.FirstOrDefault(image => FileNames.Same(StemOf(image.Path), originalStem));
    }

    private static string StemOf(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));

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

    /// <summary>
    /// Replaces <see cref="Images"/> with the current listing of the opened paths under the current settings and
    /// <see cref="ShowResults"/>; the one re-list path for <see cref="Open"/>, <see cref="ShowResults"/> and
    /// <see cref="ApplySettings"/>.
    /// </summary>
    private Listing List()
    {
        var listing = ImageListing.List(_openedPaths, ListingOptionsFor(_settings, _showResults));
        Images = listing.Images
            .Select(image => new WorkspaceImage
            {
                Path = image.Path, IsResult = image.IsResult, HasResult = image.HasResult,
            })
            .ToList();
        return listing;
    }

    private void RaiseChanged() =>
        Changed?.Invoke(this, new WorkspaceChange { Images = Images, Selected = Selected });

    private static ListingOptions ListingOptionsFor(UserSettings settings, bool includeResults) =>
        ListingOptions.For(
            new SaveOptions
            {
                Mode = settings.SaveMode,
                ResultSuffix = settings.ResultSuffix,
                BackupSuffix = settings.BackupSuffix,
            },
            includeResults);
}
