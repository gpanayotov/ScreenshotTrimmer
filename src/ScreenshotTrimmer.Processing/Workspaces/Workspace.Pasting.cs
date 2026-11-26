using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Pasted images: listed from their pixels in memory until saved, then as the file they were saved to.
public sealed partial class Workspace
{
    /// <summary>The Pasted images not saved yet, in the order they were pasted.</summary>
    private readonly List<PastedImage> _pasted = [];

    /// <summary>
    /// The Pasted images saved in this list, in the order they were saved, by the path of the file holding their
    /// Result. They stay listed whatever was opened, and render from their pixels, since their file holds the Result.
    /// </summary>
    private readonly List<SavedPasted> _savedPasted = [];

    /// <summary>
    /// The number the last Pasted image was given. It keeps counting when a new list is opened, so a number never
    /// names two different Pasted images and callers may key what they keep about an entry by its Key.
    /// </summary>
    private int _pastedCount;

    /// <summary>
    /// Adds <paramref name="pixels"/> as a Pasted image at the end of the list, selects it and announces the change. It
    /// has no file, is unsaved until <see cref="SavePasted"/> saves it, and stays listed through re-lists until then or
    /// until it is removed. The workspace keeps <paramref name="pixels"/> as the image's Original, so the caller must
    /// not change them afterwards.
    /// </summary>
    /// <returns>The new entry.</returns>
    public WorkspaceImage Paste(RgbaImage pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        WorkspaceImage? entry = null;
        Update(() =>
        {
            var pasted = new PastedImage(++_pastedCount, pixels);
            _pasted.Add(pasted);
            entry = pasted.Entry;
            Images = [.. Images, entry];
            Selected = entry;
            QueueChange();
        });
        return entry!;
    }

    /// <summary>
    /// Renders the Pasted image <paramref name="image"/>'s current Steps from its pixels and writes only the Result as a
    /// PNG to <paramref name="path"/>, replacing a file there only when <paramref name="overwrite"/> is set. On success
    /// the entry becomes the file at that path, with its Steps saved: it stays listed whatever was opened, is listed
    /// once even when the folder it was saved to is listed, and <see cref="Save"/> overwrites that file from then on.
    /// Rendering and writing run outside the workspace's lock.
    /// </summary>
    /// <exception cref="SaveFailedException">The Result could not be written; the image stays an unsaved Pasted image.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="image"/> is not listed, or is not a Pasted image that has not been saved yet.
    /// </exception>
    public SaveReport SavePasted(WorkspaceImage image, string path, bool overwrite)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(path);
        PastedImage pasted;
        Steps steps;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RequireListed(image);
            pasted = _pasted.FirstOrDefault(candidate => candidate.Number == image.PastedNumber)
                ?? throw new ArgumentException("The image is not an unsaved Pasted image.", nameof(image));
            steps = StepsOf(pasted.Key).Current;
        }

        var result = Renderer.Render(pasted.Pixels, steps).Result;
        var report = ResultFiles.SavePasted(result, path, overwrite);

        Update(() =>
        {
            if (!_pasted.Remove(pasted))
                return; // Removed from the list, or the list replaced, while it was saving.
            var savedPath = report.Written[0];
            var state = StepsOf(pasted.Key);
            _steps.Remove(pasted.Key);
            _steps[savedPath] = state with { Saved = steps };
            _savedPasted.RemoveAll(saved => FileNames.Same(saved.Path, savedPath));
            _savedPasted.Add(new SavedPasted(savedPath, pasted.Pixels));
            _backupSaves.Remove(savedPath);
            _removed.Remove(savedPath);
            var selected = Selected is { } current && current.PastedNumber == pasted.Number ? savedPath : Selected is null ? null : Selected.Key;
            Relist(_ => (selected is null ? null : Find(selected)) ?? Images.FirstOrDefault());
        });
        return report;
    }

    /// <summary>The pixels a Pasted image with the key <paramref name="key"/> renders from, saved or not; <c>null</c> for other images.</summary>
    private RgbaImage? PastedPixels(string key) =>
        _pasted.FirstOrDefault(pasted => pasted.Key == key)?.Pixels
        ?? _savedPasted.FirstOrDefault(saved => FileNames.Same(saved.Path, key))?.Pixels;

    /// <summary>The files of the saved Pasted images that still exist; call under the lock.</summary>
    private IEnumerable<string> SavedPastedPaths() =>
        _savedPasted.Select(saved => saved.Path).Where(File.Exists);

    /// <summary>The entries of the Pasted images not saved yet; call under the lock.</summary>
    private IEnumerable<WorkspaceImage> PastedEntries() => _pasted.Select(pasted => pasted.Entry);

    /// <summary>Moves the saved Pasted images whose files were renamed on disk to their new paths; call under the lock.</summary>
    private void FollowRenamesOfSavedPasted(Func<string, string> renamed)
    {
        for (var i = 0; i < _savedPasted.Count; i++)
            _savedPasted[i] = _savedPasted[i] with { Path = renamed(_savedPasted[i].Path) };
    }

    /// <summary>
    /// Forgets the saved Pasted images that left the list on disk, so a file that comes back under the same name is
    /// read from disk like any other; call under the lock after re-listing.
    /// </summary>
    private void ForgetSavedPastedNotListed() => _savedPasted.RemoveAll(saved => Find(saved.Path) is null);

    /// <summary>Drops the unsaved Pasted image <paramref name="image"/> with its pixels and Steps; call under the lock.</summary>
    private void DropPasted(WorkspaceImage image)
    {
        _pasted.RemoveAll(pasted => pasted.Number == image.PastedNumber);
        _steps.Remove(image.Key);
    }

    /// <summary>Drops every Pasted image, saved or not, as a new list is opened; call under the lock.</summary>
    private void ForgetPasted()
    {
        _pasted.Clear();
        _savedPasted.Clear();
    }

    private sealed record PastedImage(int Number, RgbaImage Pixels)
    {
        public string Key => Entry.Key;

        public WorkspaceImage Entry => new() { PastedNumber = Number, HasUnsavedSteps = true };
    }

    private sealed record SavedPasted(string Path, RgbaImage Pixels);
}
