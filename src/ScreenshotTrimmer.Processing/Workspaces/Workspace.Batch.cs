using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.Processing.Workspaces;

// The Batch: Auto crop and the remembered finishing for every listed image that needs it, as pending Steps, run in
// parallel beside the user and stopped before the list is replaced or the workspace disposed.
public sealed partial class Workspace
{
    /// <summary>The images a Batch found no Subject in, by <see cref="WorkspaceImage.Key">key</see>.</summary>
    private readonly HashSet<ImageKey> _noSubjectFound = [];

    /// <summary>The running Batch, or <c>null</c>; read and set under the lock.</summary>
    private BatchRun? _batch;

    /// <summary>
    /// Raised outside the lock when <see cref="Open"/> or <see cref="Dispose"/> has cancelled the running Batch and is
    /// about to wait for it, so a test can let a Batch it holds go on at exactly that point.
    /// </summary>
    internal event Action? BatchStopRequested;

    /// <summary>
    /// <see cref="RunBatch(IProgress{BatchProgress}?, CancellationToken, int)"/> on as many images at a time as there
    /// are processor cores.
    /// </summary>
    public BatchSummary RunBatch(IProgress<BatchProgress>? progress, CancellationToken cancellation) =>
        RunBatch(progress, cancellation, Environment.ProcessorCount);

    /// <summary>
    /// Runs one Batch over the images listed when it starts and returns what it did. Images are started in strip order,
    /// up to <paramref name="parallelism"/> at a time. Each image that <see cref="BatchSelection"/> takes gets the
    /// Subject <see cref="SubjectDetection.Find"/> finds in its Original as one Step, built with the
    /// <see cref="FirstStep"/> rule from the settings at the start, so it is unsaved and undoable like a hand Auto crop;
    /// its Steps depend only on its pixels and those settings. An image with no Subject found gets no Step and is
    /// marked <see cref="WorkspaceImage.NoSubjectFound"/>. Nothing is written to disk.
    /// </summary>
    /// <remarks>
    /// Loading and detection run outside the workspace's lock, so the list stays usable. An image is checked again
    /// before it is read and when its result is applied: one that left the list or the disk is skipped as
    /// <see cref="BatchSkip.Gone"/>, one that gained Steps as <see cref="BatchSkip.HasSteps"/>. An image that cannot be
    /// read counts as <see cref="BatchSummary.Failed"/> and the rest go on. <paramref name="progress"/> hears after
    /// each image done, on the thread that did it. Cancelling, or <see cref="Open"/> and <see cref="Dispose"/>, stop
    /// the Batch from starting more images and cancel detection in flight; the images already done keep their Steps
    /// and the summary is <see cref="BatchSummary.Cancelled"/>. Since those two wait for the Batch, they must not be
    /// called from <paramref name="progress"/> or a <see cref="Changed"/> handler on a Batch thread.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Another Batch is running in this workspace.</exception>
    public BatchSummary RunBatch(IProgress<BatchProgress>? progress, CancellationToken cancellation, int parallelism)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(parallelism, 1);
        BatchRun run;
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_batch is not null)
                throw new InvalidOperationException("A Batch is already running in this workspace.");
            run = new BatchRun(Images.Select(image => image.Key).ToList(), _settings, cancellation);
            _batch = run;
        }
        try
        {
            var next = -1;
            var done = 0;
            var progressLock = new Lock();
            void Work()
            {
                int index;
                while ((index = Interlocked.Increment(ref next)) < run.Keys.Count)
                {
                    var outcome = ProcessForBatch(run, run.Keys[index]);
                    run.Outcomes[index] = outcome;
                    if (outcome.Kind == BatchOutcomeKind.NotDone)
                        continue;
                    lock (progressLock)
                        progress?.Report(new BatchProgress(++done, run.Keys.Count));
                }
            }
            var workers = Math.Clamp(run.Keys.Count, 1, parallelism);
            Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers }, _ => Work());
            return run.Summary();
        }
        finally
        {
            lock (_lock)
                _batch = null;
            run.Finish();
        }
    }

    /// <summary>Waits for <paramref name="run"/>, already cancelled under the lock, to stop; call outside the lock.</summary>
    private void StopBatch(BatchRun run)
    {
        BatchStopRequested?.Invoke();
        run.WaitUntilFinished();
    }

    /// <summary>
    /// Runs <paramref name="change"/> as <see cref="Update"/> does, once no Batch is running: a running one is
    /// cancelled and waited for first, so it never applies a result to the changed list.
    /// </summary>
    private void UpdateWithNoBatch(Action change)
    {
        while (true)
        {
            BatchRun? running;
            lock (_lock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                running = _batch;
                if (running is null)
                {
                    change();
                    break;
                }
                running.Cancel();
            }
            StopBatch(running);
        }
        Announce();
    }

    /// <summary>One image of a Batch, from the check that it still needs processing to its applied result.</summary>
    private BatchOutcome ProcessForBatch(BatchRun run, ImageKey key)
    {
        WorkspaceImage? image;
        bool hasSteps;
        Func<RgbaImage> load;
        lock (_lock)
        {
            if (run.IsCancelled)
                return BatchOutcome.NotDone;
            image = Find(key);
            if (image is null)
                return BatchOutcome.Skipped(BatchSkip.Gone);
            hasSteps = HasAnySteps(key);
            load = OriginalLoader(key);
        }

        RgbaImage? original = null;
        try
        {
            if (BatchSelection.SkipReason(image, hasSteps, () => original = load()) is { } reason)
                return BatchOutcome.Skipped(reason);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return key.Path is { } path && !File.Exists(path)
                ? BatchOutcome.Skipped(BatchSkip.Gone)
                : BatchOutcome.Failed(image);
        }

        Subject? subject;
        try
        {
            subject = SubjectDetection.Find(original!, run.Cancellation);
        }
        catch (OperationCanceledException) when (run.IsCancelled)
        {
            return BatchOutcome.NotDone;
        }
        var steps = subject is null
            ? null
            : FirstStep.Apply(Rendering.Steps.None, run.Settings, originalHasShadowPadding: false,
                steps => steps with { Crop = subject.ToCrop() });
        return ApplyBatchResult(run, key, steps);
    }

    /// <summary>
    /// Records <paramref name="steps"/> as the image's one Step, or marks it <see cref="WorkspaceImage.NoSubjectFound"/>
    /// when they are <c>null</c>, unless the Batch was cancelled, or the image left the list or gained Steps meanwhile.
    /// </summary>
    private BatchOutcome ApplyBatchResult(BatchRun run, ImageKey key, Steps? steps)
    {
        BatchOutcome outcome;
        lock (_lock)
        {
            if (run.IsCancelled)
                return BatchOutcome.NotDone;
            if (Find(key) is not { } image)
                return BatchOutcome.Skipped(BatchSkip.Gone);
            if (HasAnySteps(key))
                return BatchOutcome.Skipped(BatchSkip.HasSteps);
            if (steps is null)
            {
                _noSubjectFound.Add(key);
                RefreshMarkers();
                outcome = BatchOutcome.NoSubjectFound(image);
            }
            else
            {
                _noSubjectFound.Remove(key);
                RecordSteps(key, steps, mergeKey: null);
                _steps[key] = StepsOf(key) with { FromBatch = true };
                outcome = BatchOutcome.Processed;
            }
        }
        Announce();
        return outcome;
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

    private enum BatchOutcomeKind { NotDone, Processed, NoSubjectFound, Skipped, Failed }

    /// <summary>What a Batch did with one image; <see cref="Image"/> is set for no Subject found and failed.</summary>
    private readonly record struct BatchOutcome(BatchOutcomeKind Kind, BatchSkip Reason = default, WorkspaceImage? Image = null)
    {
        /// <summary>The Batch stopped before it finished this image.</summary>
        public static BatchOutcome NotDone => new(BatchOutcomeKind.NotDone);

        public static BatchOutcome Processed => new(BatchOutcomeKind.Processed);

        public static BatchOutcome NoSubjectFound(WorkspaceImage image) => new(BatchOutcomeKind.NoSubjectFound, Image: image);

        public static BatchOutcome Skipped(BatchSkip reason) => new(BatchOutcomeKind.Skipped, reason);

        public static BatchOutcome Failed(WorkspaceImage image) => new(BatchOutcomeKind.Failed, Image: image);
    }

    /// <summary>One running Batch: the images and settings it started with, its cancellation, and each image's outcome.</summary>
    private sealed class BatchRun(IReadOnlyList<ImageKey> keys, UserSettings settings, CancellationToken cancellation)
    {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<ImageKey> Keys { get; } = keys;

        public UserSettings Settings { get; } = settings;

        /// <summary>Each image's outcome, by its index in <see cref="Keys"/>; each worker writes only its own images'.</summary>
        public BatchOutcome[] Outcomes { get; } = new BatchOutcome[keys.Count];

        public CancellationToken Cancellation => _stop.Token;

        public bool IsCancelled => _stop.IsCancellationRequested;

        /// <summary>Cancels the Batch; call under the workspace's lock, which keeps the run from finishing meanwhile.</summary>
        public void Cancel() => _stop.Cancel();

        public void WaitUntilFinished() => _finished.Task.Wait();

        /// <summary>Marks the run finished, once it is no longer the workspace's running Batch.</summary>
        public void Finish()
        {
            _stop.Dispose();
            _finished.SetResult();
        }

        /// <summary>The summary of the outcomes, with the listed images in strip order.</summary>
        public BatchSummary Summary() => new()
        {
            Processed = Outcomes.Count(outcome => outcome.Kind == BatchOutcomeKind.Processed),
            NoSubjectFound = ImagesWith(BatchOutcomeKind.NoSubjectFound),
            Failed = ImagesWith(BatchOutcomeKind.Failed),
            Skipped = Outcomes.Where(outcome => outcome.Kind == BatchOutcomeKind.Skipped)
                .GroupBy(outcome => outcome.Reason)
                .ToDictionary(group => group.Key, group => group.Count()),
            Cancelled = Outcomes.Any(outcome => outcome.Kind == BatchOutcomeKind.NotDone),
        };

        private List<WorkspaceImage> ImagesWith(BatchOutcomeKind kind) =>
            Outcomes.Where(outcome => outcome.Kind == kind).Select(outcome => outcome.Image!).ToList();
    }
}
