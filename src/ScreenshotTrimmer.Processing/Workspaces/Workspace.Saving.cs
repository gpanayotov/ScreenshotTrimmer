using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Rendering an image's Result from its Original and writing it to disk.
public sealed partial class Workspace
{
    /// <summary>The pixels of <paramref name="image"/>'s Original, which its Result is rendered from.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    /// <exception cref="IOException">The file cannot be read.</exception>
    /// <exception cref="InvalidDataException">The file is not an image that can be decoded.</exception>
    public RgbaImage LoadOriginal(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Images[RequireListed(image)].Path;
        }
        return ImageFile.Load(path);
    }

    /// <summary>
    /// Renders <paramref name="image"/>'s current Steps from its Original's pixels and saves the Result under the
    /// current save settings. On success the Steps that were saved become the image's saved Steps, unless they changed
    /// while it was saving, and the list is brought up to date with the new files. Rendering and writing run outside
    /// the workspace's lock, so the list can be read and changed meanwhile.
    /// </summary>
    /// <exception cref="SaveFailedException">The Result could not be written; the Steps stay unsaved.</exception>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    /// <exception cref="IOException">The Original cannot be read.</exception>
    /// <exception cref="InvalidDataException">The Original is not an image that can be decoded.</exception>
    public SaveReport Save(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        string path;
        Steps steps;
        SaveOptions options;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            path = Images[RequireListed(image)].Path;
            steps = StepsOf(path).Current;
            options = SaveOptionsFor(_settings);
        }

        var result = Renderer.Render(ImageFile.Load(path), steps).Result;
        var report = ResultFiles.Save(result, path, options);

        Update(() =>
        {
            var state = StepsOf(path);
            if (state.Current == steps)
                _steps[path] = state with { Saved = steps };
            if (_openedPaths.Count == 0)
                return;
            // The new Result is listed now rather than when the folder watcher notices it, so its badge shows at once.
            var selected = Selected;
            Relist(_ => (selected is null ? null : Find(selected.Path)) ?? Images.FirstOrDefault());
        });
        return report;
    }

    private static SaveOptions SaveOptionsFor(Settings.UserSettings settings) => new()
    {
        Mode = settings.SaveMode,
        ResultSuffix = settings.ResultSuffix,
        BackupSuffix = settings.BackupSuffix,
    };
}
