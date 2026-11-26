namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>A fact that needs the local <c>samples/</c> folder: skipped, saying why, where it is absent, as in CI.</summary>
internal sealed class SamplesFactAttribute : FactAttribute
{
    public SamplesFactAttribute()
    {
        if (SamplesFolder.Samples is null)
            Skip = "No samples/ folder at the repository root: the local samples check runs only on the maintainer's machine.";
    }
}
