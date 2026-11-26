using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>The local samples check run on a temporary samples folder of synthetic images, never the real one.</summary>
public sealed class SamplesCheckTests : IDisposable
{
    private static readonly PixelRect DialogBounds = new(137, 93, 401, 257);

    private readonly TempFolder folder = new();

    private string Samples => Path.Combine(folder.Path, "samples");
    private string Overview => Path.Combine(folder.Path, "samples-overview");
    private string AnswersFile => Path.Combine(Samples, SamplesCheck.AnswersFileName);

    public SamplesCheckTests() => Directory.CreateDirectory(Samples);

    public void Dispose() => folder.Dispose();

    private void AddDialogSample(string name)
    {
        var image = Synthetic.DimmedApplication(800, 500);
        Synthetic.DrawDialog(image, DialogBounds, 8);
        EncodedImages.WritePng(Path.Combine(Samples, name), image);
    }

    private void AddFeaturelessSample(string name) =>
        EncodedImages.WritePng(Path.Combine(Samples, name), Synthetic.Plain(300, 200, Synthetic.LightPage));

    private Dictionary<string, ApprovedAnswer> ReadAnswers() => ApprovedAnswers.Parse(File.ReadAllText(AnswersFile));

    private void WriteAnswers(Dictionary<string, ApprovedAnswer> answers) =>
        File.WriteAllText(AnswersFile, ApprovedAnswers.Format(answers));

    private SamplesCheckResult Run(Approving approving = Approving.No) => SamplesCheck.Run(Samples, Overview, approving);

    [Fact]
    public void Light_and_dark_samples_are_totalled_separately()
    {
        AddDialogSample("light-pass.png");
        AddDialogSample("light-fail.png");
        AddFeaturelessSample("dark-pass.png");
        AddDialogSample("dark-fail.png");
        AddFeaturelessSample("light-new.png");
        Run(Approving.NewSamples);
        var answers = ReadAnswers();
        answers["light-fail.png"] = answers["light-fail.png"] with { Subject = new Subject(new PixelRect(1, 2, 3, 4)) };
        answers["dark-pass.png"] = answers["dark-pass.png"] with { Dark = true };
        answers["dark-fail.png"] = ApprovedAnswer.AlreadyCroppedAnswer(answers["dark-fail.png"].Fingerprint) with { Dark = true };
        answers.Remove("light-new.png");
        WriteAnswers(answers);

        var result = Run();

        Assert.Equal(new SampleTotals(Pass: 1, Fail: 1, New: 1, Changed: 0), result.Light);
        Assert.Equal(new SampleTotals(Pass: 1, Fail: 1, New: 0, Changed: 0), result.Dark);
        Assert.Contains("Light: 3 samples: 1 pass, 1 fail, 1 new, 0 changed", result.Report);
        Assert.Contains("Dark: 2 samples: 1 pass, 1 fail, 0 new, 0 changed", result.Report);
    }

    [Fact]
    public void Regenerating_keeps_hand_set_answers_tolerances_and_dark_flags_and_fills_in_only_new_samples()
    {
        AddDialogSample("hand-entered.png");
        AddFeaturelessSample("cropped.png");
        AddDialogSample("flagged.png");
        Run(Approving.NewSamples);
        var answers = ReadAnswers();
        var handEntered = answers["hand-entered.png"] with { Subject = new Subject(new PixelRect(1, 2, 3, 4)) };
        var cropped = ApprovedAnswer.AlreadyCroppedAnswer(answers["cropped.png"].Fingerprint);
        var flagged = answers["flagged.png"] with { Tolerance = 1, Dark = true };
        WriteAnswers(new() { ["hand-entered.png"] = handEntered, ["cropped.png"] = cropped, ["flagged.png"] = flagged });
        AddDialogSample("added.png");

        Run(Approving.NewSamples);

        var regenerated = ReadAnswers();
        Assert.Equal(handEntered, regenerated["hand-entered.png"]);
        Assert.Equal(cropped, regenerated["cropped.png"]);
        Assert.Equal(flagged, regenerated["flagged.png"]);
        Assert.Equal(new Subject(DialogBounds), regenerated["added.png"].Subject);
        Assert.Equal(4, regenerated.Count);
    }

    [Fact]
    public void Regenerating_everything_takes_detections_answers_and_keeps_only_the_dark_flags()
    {
        AddDialogSample("dialog.png");
        AddFeaturelessSample("empty.png");
        Run(Approving.NewSamples);
        var answers = ReadAnswers();
        WriteAnswers(new()
        {
            ["dialog.png"] = answers["dialog.png"] with { Subject = new Subject(new PixelRect(1, 2, 3, 4)), Tolerance = 1, Dark = true },
            ["empty.png"] = ApprovedAnswer.AlreadyCroppedAnswer(answers["empty.png"].Fingerprint),
            ["gone.png"] = new ApprovedAnswer("ffff", null),
        });

        Run(Approving.Everything);

        var regenerated = ReadAnswers();
        Assert.Equal(answers["dialog.png"] with { Dark = true }, regenerated["dialog.png"]);
        Assert.Equal(answers["empty.png"], regenerated["empty.png"]);
        Assert.Equal(2, regenerated.Count);
    }

    [Fact]
    public void Without_approving_the_answers_file_is_left_as_it_is()
    {
        AddDialogSample("dialog.png");
        File.WriteAllText(AnswersFile, "# hand-written\n");

        Run();

        Assert.Equal("# hand-written\n", File.ReadAllText(AnswersFile));
    }
}
