using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>Whether a run of the samples check writes the approved-answers file, and how much of it.</summary>
internal enum Approving
{
    /// <summary>Only check; the answers file is left as it is.</summary>
    No,

    /// <summary>Add detection's answer for each new sample; every existing entry, hand-set or not, is kept as it is.</summary>
    NewSamples,

    /// <summary>Replace every answer and tolerance with this run's output, keeping only the dark flags.</summary>
    Everything,
}

/// <summary>How many samples of one theme passed, failed, are new or changed.</summary>
internal sealed record SampleTotals(int Pass, int Fail, int New, int Changed)
{
    public int Count => Pass + Fail + New + Changed;
}

internal sealed record SamplesCheckResult(string Report, SampleTotals Light, SampleTotals Dark)
{
    public int Failed => Light.Fail + Dark.Fail;
}

/// <summary>
/// Runs <see cref="SubjectDetection.Find"/> on every image in a samples folder and compares each answer with the approved
/// one in that folder's answers file (format: <see cref="ApprovedAnswers"/>). Writes an overview picture per sample and
/// the report to the overview folder, and returns the report with light and dark totals.
/// </summary>
internal static class SamplesCheck
{
    public const string AnswersFileName = "approved-answers.txt";

    private static readonly HashSet<string> ImageExtensions =
        new([".png", ".jpg", ".jpeg", ".webp", ".bmp"], StringComparer.OrdinalIgnoreCase);

    private sealed record Outcome(string Name, SampleVerdict Verdict, TimeSpan Took, Finding Found, string Fingerprint, ApprovedAnswer? Approved)
    {
        public bool Dark => Approved?.Dark ?? false;
    }

    public static SamplesCheckResult Run(string samples, string overview, Approving approving)
    {
        var answersFile = Path.Combine(samples, AnswersFileName);
        var approved = File.Exists(answersFile) ? ApprovedAnswers.Parse(File.ReadAllText(answersFile)) : [];
        PrepareOverviewFolder(overview);

        var outcomes = new List<Outcome>();
        foreach (var path in SampleImages(samples))
        {
            var name = Path.GetFileName(path);
            var fingerprint = Fingerprint(path);
            var image = ImageFile.Load(path);

            var clock = Stopwatch.StartNew();
            var found = SubjectDetection.Find(image);
            clock.Stop();

            var answer = approved.GetValueOrDefault(name);
            var verdict = SampleVerdicts.Judge(answer, fingerprint, found);
            outcomes.Add(new Outcome(name, verdict, clock.Elapsed, found, fingerprint, answer));
            SampleOverview.Write(
                Path.Combine(overview, Path.GetFileNameWithoutExtension(name) + ".overview.png"),
                image, found.Subject, verdict == SampleVerdict.Fail ? answer?.Subject : null);
        }

        if (approving != Approving.No)
            File.WriteAllText(answersFile, ApprovedAnswers.Format(Regenerate(approved, outcomes, approving)));

        var gone = approved.Keys.Where(name => outcomes.All(o => o.Name != name)).Order(StringComparer.Ordinal);
        var light = Totals(outcomes.Where(o => !o.Dark));
        var dark = Totals(outcomes.Where(o => o.Dark));
        var report = Report(outcomes, gone, light, dark, approving);
        File.WriteAllText(Path.Combine(overview, "report.txt"), report);
        return new SamplesCheckResult(report, light, dark);
    }

    private static Dictionary<string, ApprovedAnswer> Regenerate(
        Dictionary<string, ApprovedAnswer> approved, List<Outcome> outcomes, Approving approving)
    {
        if (approving == Approving.Everything)
            return outcomes.ToDictionary(o => o.Name, o => Answer(o) with { Dark = o.Dark }, StringComparer.Ordinal);

        var answers = new Dictionary<string, ApprovedAnswer>(approved, StringComparer.Ordinal);
        foreach (var o in outcomes.Where(o => o.Approved is null))
            answers[o.Name] = Answer(o);
        return answers;
    }

    /// <summary>Detection's answer for the sample of <paramref name="outcome"/>, as it would be approved.</summary>
    private static ApprovedAnswer Answer(Outcome outcome) =>
        outcome.Found.IsAlreadyCropped
            ? ApprovedAnswer.AlreadyCroppedAnswer(outcome.Fingerprint)
            : new ApprovedAnswer(outcome.Fingerprint, outcome.Found.Subject);

    private static IEnumerable<string> SampleImages(string samples) =>
        Directory.EnumerateFiles(samples)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .Order(StringComparer.Ordinal);

    private static void PrepareOverviewFolder(string overview)
    {
        Directory.CreateDirectory(overview);
        foreach (var stale in Directory.EnumerateFiles(overview, "*.overview.png"))
            File.Delete(stale);
    }

    private static string Fingerprint(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(file))[..16];
    }

    private static SampleTotals Totals(IEnumerable<Outcome> outcomes)
    {
        var verdicts = outcomes.Select(o => o.Verdict).ToList();
        int Count(SampleVerdict verdict) => verdicts.Count(v => v == verdict);
        return new SampleTotals(Count(SampleVerdict.Pass), Count(SampleVerdict.Fail), Count(SampleVerdict.New), Count(SampleVerdict.Changed));
    }

    private static string Report(
        List<Outcome> outcomes, IEnumerable<string> gone, SampleTotals light, SampleTotals dark, Approving approving)
    {
        var text = new StringBuilder();
        foreach (var o in outcomes)
        {
            text.Append(CultureInfo.InvariantCulture, $"{Label(o.Verdict),-7} {o.Took.TotalMilliseconds,7:0} ms  {(o.Dark ? "dark " : "")}{o.Name}");
            text.Append(o.Verdict switch
            {
                SampleVerdict.Fail => $"\n        expected {ApprovedAnswers.Describe(o.Approved!)}\n        found    {ApprovedAnswers.Describe(o.Found)}",
                SampleVerdict.Changed => $"\n        approved {ApprovedAnswers.Describe(o.Approved!)} for an earlier file\n"
                    + $"        found    {ApprovedAnswers.Describe(o.Found)} in the file with fingerprint {o.Fingerprint}",
                _ => $": {ApprovedAnswers.Describe(o.Found)}",
            });
            text.Append('\n');
        }
        foreach (var name in gone)
            text.Append(CultureInfo.InvariantCulture, $"GONE              {name}: approved, but no longer in samples/\n");

        text.Append('\n');
        Totals(text, "Light", light, outcomes.Where(o => !o.Dark));
        Totals(text, "Dark", dark, outcomes.Where(o => o.Dark));
        text.Append(approving switch
        {
            Approving.NewSamples => "Answers added for new samples; existing answers kept. Check the new ones' overview pictures by eye.\n",
            Approving.Everything => "Every answer regenerated from this run's output (dark flags kept). Check the overview pictures by eye.\n",
            _ => "New samples are not failures: add their answers with APPROVE_SAMPLES=1. Changed samples need their "
                + "answer checked and their fingerprint updated by hand, or APPROVE_SAMPLES=overwrite.\n",
        });
        return text.ToString();
    }

    private static void Totals(StringBuilder text, string theme, SampleTotals totals, IEnumerable<Outcome> outcomes)
    {
        text.Append(CultureInfo.InvariantCulture,
            $"{theme}: {totals.Count} samples: {totals.Pass} pass, {totals.Fail} fail, {totals.New} new, {totals.Changed} changed.");
        var times = outcomes.Select(o => o.Took.TotalMilliseconds).ToList();
        if (times.Count > 0)
            text.Append(CultureInfo.InvariantCulture, $" Time per image {times.Min():0}–{times.Max():0} ms, total {times.Sum():0} ms.");
        text.Append('\n');
    }

    private static string Label(SampleVerdict verdict) => verdict switch
    {
        SampleVerdict.Pass => "PASS",
        SampleVerdict.Fail => "FAIL",
        SampleVerdict.New => "NEW",
        _ => "CHANGED",
    };
}
