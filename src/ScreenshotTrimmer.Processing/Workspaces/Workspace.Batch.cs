using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

// The Batch: Auto crop and the remembered finishing for every listed image that needs it, as pending Steps.
public sealed partial class Workspace
{
    /// <summary>The images a Batch found no Subject in, by <see cref="WorkspaceImage.Key">key</see>.</summary>
    private readonly HashSet<ImageKey> _noSubjectFound = [];

    /// <summary>
    /// Runs one Batch over the images listed when it starts, in strip order, and returns what it did. Each image that
    /// <see cref="BatchSelection"/> takes gets the Subject <see cref="SubjectDetection.Find"/> finds in its Original as
    /// one Step, built with the <see cref="FirstStep"/> rule from the remembered settings, so it is unsaved and
    /// undoable like a hand Auto crop. An image with no Subject found gets no Step and is marked
    /// <see cref="WorkspaceImage.NoSubjectFound"/>. Nothing is written to disk. Loading and detection run outside the
    /// workspace's lock; <paramref name="progress"/> hears after each listed image.
    /// </summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellation"/> was cancelled.</exception>
    public BatchSummary RunBatch(IProgress<BatchProgress>? progress, CancellationToken cancellation)
    {
        List<(WorkspaceImage Image, bool HasSteps)> images;
        UserSettings settings;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            settings = _settings;
            images = Images.Select(image => (image, HasAnySteps(image.Key))).ToList();
        }

        var processed = 0;
        var noSubjectFound = new List<WorkspaceImage>();
        var skipped = new Dictionary<BatchSkip, int>();
        for (var i = 0; i < images.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var (image, hasSteps) = images[i];
            RgbaImage? original = null;
            if (BatchSelection.SkipReason(image, hasSteps, () => original = LoadOriginal(image)) is { } reason)
            {
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
            }
            else
            {
                var subject = SubjectDetection.Find(original!, cancellation);
                var steps = subject is null
                    ? null
                    : FirstStep.Apply(Rendering.Steps.None, settings, originalHasShadowPadding: false,
                        steps => steps with { Crop = subject.ToCrop() });
                if (ApplyBatchResult(image.Key, steps))
                {
                    if (steps is null)
                        noSubjectFound.Add(image);
                    else
                        processed++;
                }
            }
            progress?.Report(new BatchProgress(i + 1, images.Count));
        }
        return new BatchSummary { Processed = processed, NoSubjectFound = noSubjectFound, Skipped = skipped };
    }

    /// <summary>
    /// Records <paramref name="steps"/> as the image's one Step, or marks it <see cref="WorkspaceImage.NoSubjectFound"/>
    /// when they are <c>null</c>, unless it left the list or gained Steps meanwhile.
    /// </summary>
    /// <returns>Whether the result was applied.</returns>
    private bool ApplyBatchResult(ImageKey key, Steps? steps)
    {
        var applied = false;
        Update(() =>
        {
            if (Find(key) is null || HasAnySteps(key))
                return;
            applied = true;
            if (steps is null)
            {
                _noSubjectFound.Add(key);
                RefreshMarkers();
                return;
            }
            _noSubjectFound.Remove(key);
            RecordSteps(key, steps, mergeKey: null);
        });
        return applied;
    }

    /// <summary>Whether the image with the key <paramref name="key"/> has Steps, saved or not; call under the lock.</summary>
    private bool HasAnySteps(ImageKey key) =>
        StepsOf(key) is var state && (state.Current != Rendering.Steps.None || state.Saved != Rendering.Steps.None);

    /// <summary>Moves the No Subject found flags of files renamed on disk to their new paths; call under the lock.</summary>
    private void FollowRenamesOfNoSubjectFound(Func<string, string> renamed)
    {
        var keys = _noSubjectFound.Select(key => key.Renamed(renamed)).ToList();
        _noSubjectFound.Clear();
        _noSubjectFound.UnionWith(keys);
    }
}
