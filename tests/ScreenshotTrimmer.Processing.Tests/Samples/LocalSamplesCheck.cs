using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;
using Xunit.Abstractions;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>
/// The local samples check: runs <see cref="SubjectDetection.Find"/> on every image in the maintainer's git-ignored
/// <c>samples/</c> folder and compares each answer with the approved one. It reports pass, fail, new or changed with the
/// time taken per image, writes an overview picture per sample and the report to <c>samples-overview/</c>, and fails
/// only on a fail. Skipped where <c>samples/</c> is absent, as in CI.
/// <para>
/// To run it with the report shown: <c>dotnet test --filter LocalSamplesCheck --logger "console;verbosity=detailed"</c>.
/// To (re)generate the approved answers from the current output, set <c>APPROVE_SAMPLES=1</c> for that run, then check
/// the overview pictures by eye.
/// </para>
/// </summary>
public class LocalSamplesCheck(ITestOutputHelper output)
{
    private static readonly HashSet<string> ImageExtensions =
        new([".png", ".jpg", ".jpeg", ".webp", ".bmp"], StringComparer.OrdinalIgnoreCase);

    private sealed record Outcome(string Name, SampleVerdict Verdict, TimeSpan Took, Subject? Found, ApprovedAnswer? Approved);

    [SamplesFact]
    public void Detection_on_the_real_samples_finds_the_approved_answers()
    {
        var approving = Environment.GetEnvironmentVariable("APPROVE_SAMPLES") == "1";
        var approved = File.Exists(SamplesFolder.ApprovedAnswersFile)
            ? ApprovedAnswers.Parse(File.ReadAllText(SamplesFolder.ApprovedAnswersFile))
            : [];
        PrepareOverviewFolder();

        var outcomes = new List<Outcome>();
        var regenerated = new Dictionary<string, ApprovedAnswer>(StringComparer.Ordinal);
        foreach (var path in SampleImages())
        {
            var name = Path.GetFileName(path);
            var fingerprint = Fingerprint(path);
            var image = ImageFile.Load(path);

            var clock = Stopwatch.StartNew();
            var found = SubjectDetection.Find(image);
            clock.Stop();

            var answer = approved.GetValueOrDefault(name);
            var verdict = SampleVerdicts.Judge(answer, fingerprint, found);
            outcomes.Add(new Outcome(name, verdict, clock.Elapsed, found, answer));
            regenerated[name] = new ApprovedAnswer(fingerprint, found);
            SampleOverview.Write(
                Path.Combine(SamplesFolder.Overview, Path.GetFileNameWithoutExtension(name) + ".overview.png"),
                image, found, verdict == SampleVerdict.Fail ? answer?.Subject : null);
        }

        if (approving)
            File.WriteAllText(SamplesFolder.ApprovedAnswersFile, ApprovedAnswers.Format(regenerated));

        var report = Report(outcomes, approved.Keys.Where(name => !regenerated.ContainsKey(name)), approving);
        File.WriteAllText(Path.Combine(SamplesFolder.Overview, "report.txt"), report);
        output.WriteLine(report);

        var failed = outcomes.Count(o => o.Verdict == SampleVerdict.Fail);
        if (failed > 0 && !approving)
            Assert.Fail($"{failed} sample(s) no longer give the approved answer.\n{report}");
    }

    private static IEnumerable<string> SampleImages() =>
        Directory.EnumerateFiles(SamplesFolder.Samples!)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .Order(StringComparer.Ordinal);

    private static void PrepareOverviewFolder()
    {
        Directory.CreateDirectory(SamplesFolder.Overview);
        foreach (var stale in Directory.EnumerateFiles(SamplesFolder.Overview, "*.overview.png"))
            File.Delete(stale);
    }

    private static string Fingerprint(string path)
    {
        using var file = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(file))[..16];
    }

    private static string Report(List<Outcome> outcomes, IEnumerable<string> gone, bool approving)
    {
        var text = new StringBuilder();
        foreach (var o in outcomes)
        {
            text.Append(CultureInfo.InvariantCulture, $"{Label(o.Verdict),-7} {o.Took.TotalMilliseconds,7:0} ms  {o.Name}");
            text.Append(o.Verdict switch
            {
                SampleVerdict.Fail => $"\n        expected {ApprovedAnswers.Describe(o.Approved!.Subject)}\n        found    {ApprovedAnswers.Describe(o.Found)}",
                SampleVerdict.Changed => $"\n        approved {ApprovedAnswers.Describe(o.Approved!.Subject)} for an earlier file\n        found    {ApprovedAnswers.Describe(o.Found)}",
                _ => $": {ApprovedAnswers.Describe(o.Found)}",
            });
            text.Append('\n');
        }
        foreach (var name in gone)
            text.Append(CultureInfo.InvariantCulture, $"GONE              {name}: approved, but no longer in samples/\n");

        int Count(SampleVerdict verdict) => outcomes.Count(o => o.Verdict == verdict);
        text.Append(CultureInfo.InvariantCulture,
            $"\n{outcomes.Count} samples: {Count(SampleVerdict.Pass)} pass, {Count(SampleVerdict.Fail)} fail, "
            + $"{Count(SampleVerdict.New)} new, {Count(SampleVerdict.Changed)} changed.");
        if (outcomes.Count > 0)
            text.Append(CultureInfo.InvariantCulture,
                $" Time per image {outcomes.Min(o => o.Took.TotalMilliseconds):0}–{outcomes.Max(o => o.Took.TotalMilliseconds):0} ms, "
                + $"total {outcomes.Sum(o => o.Took.TotalMilliseconds):0} ms.");
        text.Append('\n');
        text.Append(approving
            ? "Approved answers regenerated from this run's output: check the overview pictures in samples-overview/ by eye.\n"
            : "New and changed samples are not failures; approve them by running with APPROVE_SAMPLES=1.\n");
        return text.ToString();
    }

    private static string Label(SampleVerdict verdict) => verdict switch
    {
        SampleVerdict.Pass => "PASS",
        SampleVerdict.Fail => "FAIL",
        SampleVerdict.New => "NEW",
        _ => "CHANGED",
    };
}
