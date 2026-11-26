using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Planning a Batch: what one started now would process and skip; RunBatch starts from the same plan.
public sealed partial class Workspace
{
    /// <summary>
    /// Whether each file version planned so far is already processed, so planning again, as the app does after every
    /// change, decodes only new or rewritten files. A rewritten file has a new content version.
    /// </summary>
    private readonly Dictionary<(ImageKey Key, ContentVersion Version), bool> _alreadyProcessedFiles = [];

    /// <summary>
    /// What a Batch started now would do: the listed images <see cref="BatchSelection"/> takes, in strip order, and how
    /// many it skips for each reason. Originals are loaded outside the workspace's lock, only for images the cheaper
    /// checks leave in.
    /// </summary>
    public BatchPlan PlanBatch()
    {
        List<(WorkspaceImage Image, bool HasSteps)> images;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            images = ListedWithSteps();
        }
        return Plan(images, Environment.ProcessorCount, CancellationToken.None);
    }

    /// <summary>Each listed image with whether it has Steps, saved or not, in strip order; call under the lock.</summary>
    private List<(WorkspaceImage Image, bool HasSteps)> ListedWithSteps() =>
        Images.Select(image => (image, HasAnySteps(image.Key))).ToList();

    /// <summary>
    /// The plan for <paramref name="images"/>, checking up to <paramref name="parallelism"/> of them at a time; call
    /// outside the lock. Once <paramref name="cancellation"/> is cancelled, the images not checked yet are taken
    /// unchecked, for the Batch to find it was cancelled.
    /// </summary>
    private BatchPlan Plan(
        IReadOnlyList<(WorkspaceImage Image, bool HasSteps)> images, int parallelism, CancellationToken cancellation)
    {
        var reasons = new BatchSkip?[images.Count];
        Parallel.For(0, images.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
        {
            if (!cancellation.IsCancellationRequested)
                reasons[i] = SkipReasonForPlan(images[i].Image, images[i].HasSteps);
        });
        return new BatchPlan
        {
            Images = images.Where((_, i) => reasons[i] is null).Select(image => image.Image).ToList(),
            Skipped = reasons.OfType<BatchSkip>().GroupBy(reason => reason).ToDictionary(group => group.Key, group => group.Count()),
        };
    }

    /// <summary>
    /// Why a Batch would skip <paramref name="image"/>, or <c>null</c> when it would take it, including when its
    /// Original cannot be read: the Batch tries it and reports it.
    /// </summary>
    private BatchSkip? SkipReasonForPlan(WorkspaceImage image, bool hasSteps)
    {
        try
        {
            if (image.IsPasted)
                return BatchSelection.SkipReason(image, hasSteps, () => LoadOriginal(image));
            var version = (image.Key, image.ContentVersion);
            bool? known;
            lock (_lock)
                known = _alreadyProcessedFiles.TryGetValue(version, out var processed) ? processed : null;
            var reason = BatchSelection.SkipReason(image, hasSteps,
                () => known ?? AlreadyProcessed.HasShadowPadding(LoadOriginal(image)));
            if (known is null && reason is null or BatchSkip.AlreadyProcessed)
            {
                lock (_lock)
                    _alreadyProcessedFiles[version] = reason is BatchSkip.AlreadyProcessed;
            }
            return reason;
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return null;
        }
        catch (Exception e) when (e is ArgumentException or ObjectDisposedException)
        {
            // The image left the list, or the workspace closed, while it was checked: a Batch finds that out itself.
            return null;
        }
    }

    /// <summary>Whether <paramref name="e"/> says an Original could not be read, rather than that something is wrong.</summary>
    private static bool IsUnreadable(Exception e) =>
        e is IOException or InvalidDataException or UnauthorizedAccessException;
}
