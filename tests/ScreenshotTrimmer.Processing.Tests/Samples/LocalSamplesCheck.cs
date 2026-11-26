using Xunit.Abstractions;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>
/// The local samples check: runs <see cref="SamplesCheck"/> on the maintainer's git-ignored <c>samples/</c> folder,
/// reporting pass, fail, new or changed with the time taken per image and totals for light and dark samples, and writing
/// an overview picture per sample and the report to <c>samples-overview/</c>. Fails only on a fail. Skipped where
/// <c>samples/</c> is absent, as in CI.
/// <para>
/// To run it with the report shown: <c>dotnet test --filter LocalSamplesCheck --logger "console;verbosity=detailed"</c>.
/// </para>
/// <para>
/// The approved answers live in <c>samples/approved-answers.txt</c>, one line per sample, tab separated:
/// <c>file name, fingerprint, answer</c>. The answer is <c>none</c>, <c>already-cropped</c>, or
/// <c>window x,y,w,h</c> followed by a <c>popup x,y,w,h</c> per popup; then optionally <c>tolerance 1</c> (each edge may
/// be 1px off) and then optionally <c>dark</c> (counted in the dark totals). <c>#</c> lines are comments, lost when the
/// file is rewritten. For example: <c>menu.png	1a2b…	window 100,50,400,300 popup 480,120,160,200 tolerance 1 dark</c>.
/// </para>
/// <para>
/// <c>APPROVE_SAMPLES=1</c> adds detection's answer for new samples only, keeping every existing line, hand-set or not.
/// <c>APPROVE_SAMPLES=overwrite</c> replaces every answer and tolerance with detection's output, keeping only the dark
/// flags. Either way, check the overview pictures by eye.
/// </para>
/// </summary>
public class LocalSamplesCheck(ITestOutputHelper output)
{
    [SamplesFact]
    public void Detection_on_the_real_samples_finds_the_approved_answers()
    {
        var approving = Environment.GetEnvironmentVariable("APPROVE_SAMPLES") switch
        {
            "1" => Approving.NewSamples,
            "overwrite" => Approving.Everything,
            null or "" or "0" => Approving.No,
            var other => throw new InvalidOperationException($"APPROVE_SAMPLES must be 1 or overwrite, not \"{other}\"."),
        };

        var result = SamplesCheck.Run(SamplesFolder.Samples!, SamplesFolder.Overview, approving);
        output.WriteLine(result.Report);

        if (result.Failed > 0 && approving == Approving.No)
            Assert.Fail($"{result.Failed} sample(s) no longer give the approved answer.\n{result.Report}");
    }
}
