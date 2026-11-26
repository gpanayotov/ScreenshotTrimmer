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
}
