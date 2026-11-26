using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Each image's pending Steps and the Steps at its last successful save.
public sealed partial class Workspace
{
    /// <summary>The images with Steps, by <see cref="KeyOf">key</see>; an image missing here has <c>Steps.None</c> and was never saved.</summary>
    private readonly Dictionary<string, ImageSteps> _steps = new(FileNames.Comparer);

    /// <summary>
    /// Whether any listed image has unsaved Steps, so leaving, or replacing the list, would lose work.
    /// </summary>
    public bool HasUnsavedWork
    {
        get
        {
            lock (_lock)
                return Images.Any(image => image.HasUnsavedSteps);
        }
    }

    /// <summary><paramref name="image"/>'s current Steps; <c>Steps.None</c> for an image with none.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public Steps Steps(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RequireListed(image);
            return StepsOf(KeyOf(image)).Current;
        }
    }

    /// <summary>
    /// Makes <paramref name="steps"/> <paramref name="image"/>'s current Steps and announces the change. Steps equal to
    /// the current ones change nothing.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public void SetSteps(WorkspaceImage image, Steps steps)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(steps);
        Update(() =>
        {
            RequireListed(image);
            var state = StepsOf(KeyOf(image));
            if (state.Current == steps)
                return;
            _steps[KeyOf(image)] = state with { Current = steps };
            RefreshUnsavedMarkers();
        });
    }

    private ImageSteps StepsOf(string path) => _steps.GetValueOrDefault(path) ?? ImageSteps.New;

    private bool HasUnsavedSteps(string key) => StepsOf(key) is var state && state.Current != state.Saved;

    /// <summary>
    /// Brings every listed entry's <see cref="WorkspaceImage.HasUnsavedSteps"/> up to date and queues a change when one
    /// changed; call under the lock.
    /// </summary>
    private void RefreshUnsavedMarkers()
    {
        var images = Images
            .Select(image => image with { HasUnsavedSteps = image.IsPasted || HasUnsavedSteps(KeyOf(image)) })
            .ToList();
        if (images.SequenceEqual(Images))
            return;
        Images = images;
        Selected = Selected is null ? null : Find(KeyOf(Selected));
        QueueChange();
    }

    /// <summary>Moves the Steps of files renamed on disk to their new paths; call under the lock.</summary>
    private void FollowRenamesWithSteps(Func<string, string> renamed)
    {
        foreach (var (from, state) in _steps.ToList())
        {
            var to = renamed(from);
            if (FileNames.Same(from, to))
                continue;
            _steps.Remove(from);
            _steps[to] = state;
        }
    }

    /// <summary>
    /// Discards the Steps of the files in <paramref name="before"/> that are no longer listed, so a file that comes
    /// back under the same name starts afresh; call under the lock after re-listing.
    /// </summary>
    /// <returns>The paths of the dropped images that had unsaved Steps, in strip order.</returns>
    private List<string> DiscardStepsOfDropped(IEnumerable<string> before)
    {
        var gone = before.Where(path => Find(path) is null).ToList();
        var unsaved = gone.Where(HasUnsavedSteps).ToList();
        foreach (var path in gone)
            _steps.Remove(path);
        return unsaved;
    }

    /// <param name="Current">The Steps the image has now.</param>
    /// <param name="Saved">The Steps at the image's last successful save; <c>Steps.None</c> until then.</param>
    private sealed record ImageSteps(Steps Current, Steps Saved)
    {
        public static ImageSteps New { get; } = new(Rendering.Steps.None, Rendering.Steps.None);
    }
}
