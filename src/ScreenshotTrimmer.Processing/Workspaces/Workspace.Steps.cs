using System.Collections.Immutable;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Each image's pending Steps, their undo history and the Steps at its last successful save.
public sealed partial class Workspace
{
    /// <summary>The images with Steps, by path; an image missing here has <c>Steps.None</c> and was never saved.</summary>
    private readonly Dictionary<string, ImageSteps> _steps = new(FileNames.Comparer);

    /// <summary><paramref name="image"/>'s current Steps; <c>Steps.None</c> for an image with none.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public Steps Steps(WorkspaceImage image) => Read(image).Current;

    /// <summary>
    /// Makes <paramref name="steps"/> <paramref name="image"/>'s current Steps, as one Step <see cref="Undo"/> goes back
    /// from, and announces the change. Steps equal to the current ones change nothing. A new Step clears what was undone.
    /// </summary>
    /// <param name="image">The image to change.</param>
    /// <param name="steps">The image's new Steps.</param>
    /// <param name="mergeKey">
    /// When not <c>null</c> and equal to the previous change's key, as for a run of arrow-key nudges on the same handle,
    /// <paramref name="steps"/> replaces that change's Steps instead of adding a Step, so the whole run is undone at once.
    /// An undo, a redo, a save or a change without this key ends the run.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public void SetSteps(WorkspaceImage image, Steps steps, object? mergeKey = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(steps);
        Update(() =>
        {
            RequireListed(image);
            var state = StepsOf(image.Path);
            if (state.Current == steps)
                return;
            var merging = mergeKey is not null && Equals(mergeKey, state.MergeKey) && !state.Earlier.IsEmpty;
            var earlier = merging ? state.Earlier : state.Earlier.Push(state.Current);
            _steps[image.Path] = earlier.Peek() == steps
                // A run that came back to where it started has nothing left to undo.
                ? state with { Current = steps, Earlier = earlier.Pop(), Later = [], MergeKey = null }
                : state with { Current = steps, Earlier = earlier, Later = [], MergeKey = mergeKey };
            AnnounceSteps();
        });
    }

    /// <summary>
    /// Makes <paramref name="image"/>'s previous Steps current again and announces the change; nothing happens when it
    /// has nothing to undo.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public void Undo(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Update(() =>
        {
            RequireListed(image);
            var state = StepsOf(image.Path);
            if (state.Earlier.IsEmpty)
                return;
            _steps[image.Path] = state with
            {
                Current = state.Earlier.Peek(), Earlier = state.Earlier.Pop(), Later = state.Later.Push(state.Current),
                MergeKey = null,
            };
            AnnounceSteps();
        });
    }

    /// <summary>
    /// Makes the Steps <paramref name="image"/> had before its last <see cref="Undo"/> current again and announces the
    /// change; nothing happens when it has nothing to redo.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public void Redo(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Update(() =>
        {
            RequireListed(image);
            var state = StepsOf(image.Path);
            if (state.Later.IsEmpty)
                return;
            _steps[image.Path] = state with
            {
                Current = state.Later.Peek(), Earlier = state.Earlier.Push(state.Current), Later = state.Later.Pop(),
                MergeKey = null,
            };
            AnnounceSteps();
        });
    }

    /// <summary>
    /// Removes all of <paramref name="image"/>'s Steps as one Step <see cref="Undo"/> brings back, and announces the
    /// change; nothing happens when it has none.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public void Reset(WorkspaceImage image) => SetSteps(image, Rendering.Steps.None);

    /// <summary>Whether <paramref name="image"/> has Steps to <see cref="Undo"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public bool CanUndo(WorkspaceImage image) => !Read(image).Earlier.IsEmpty;

    /// <summary>Whether <paramref name="image"/> has undone Steps to <see cref="Redo"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="image"/> is not listed.</exception>
    public bool CanRedo(WorkspaceImage image) => !Read(image).Later.IsEmpty;

    private ImageSteps Read(WorkspaceImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            RequireListed(image);
            return StepsOf(image.Path);
        }
    }

    private ImageSteps StepsOf(string path) => _steps.GetValueOrDefault(path) ?? ImageSteps.New;

    private bool HasUnsavedSteps(string path) => StepsOf(path) is var state && state.Current != state.Saved;

    /// <summary>Announces a change of Steps, with the unsaved markers brought up to date; call under the lock.</summary>
    private void AnnounceSteps()
    {
        if (!RefreshUnsavedMarkers())
            QueueChange();
    }

    /// <summary>
    /// Brings every listed entry's <see cref="WorkspaceImage.HasUnsavedSteps"/> up to date and queues a change when one
    /// changed; call under the lock.
    /// </summary>
    /// <returns>Whether a marker changed, and so a change was queued.</returns>
    private bool RefreshUnsavedMarkers()
    {
        var images = Images.Select(image => image with { HasUnsavedSteps = HasUnsavedSteps(image.Path) }).ToList();
        if (images.SequenceEqual(Images))
            return false;
        Images = images;
        Selected = Selected is null ? null : Find(Selected.Path);
        QueueChange();
        return true;
    }

    /// <param name="Current">The Steps the image has now.</param>
    /// <param name="Saved">The Steps at the image's last successful save; <c>Steps.None</c> until then.</param>
    /// <param name="Earlier">The Steps <see cref="Undo"/> goes back to, the most recent on top.</param>
    /// <param name="Later">The Steps <see cref="Redo"/> goes forward to, the next on top.</param>
    /// <param name="MergeKey">The merge key of the last change, while a run of merged changes may continue.</param>
    private sealed record ImageSteps(
        Steps Current, Steps Saved, ImmutableStack<Steps> Earlier, ImmutableStack<Steps> Later, object? MergeKey)
    {
        public static ImageSteps New { get; } = new(Rendering.Steps.None, Rendering.Steps.None, [], [], null);
    }
}
