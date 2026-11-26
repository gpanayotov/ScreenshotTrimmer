using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Workspaces;

// Planning a Batch: what one started now would process and skip, by the same rules as RunBatch.
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
            images = Images.Select(image => (image, HasAnySteps(image.Key))).ToList();
        }

        var taken = new List<WorkspaceImage>();
        var skipped = new Dictionary<BatchSkip, int>();
        foreach (var (image, hasSteps) in images)
        {
            if (SkipReasonForPlan(image, hasSteps) is { } reason)
                skipped[reason] = skipped.GetValueOrDefault(reason) + 1;
            else
                taken.Add(image);
        }
        return new BatchPlan { Images = taken, Skipped = skipped };
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
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
