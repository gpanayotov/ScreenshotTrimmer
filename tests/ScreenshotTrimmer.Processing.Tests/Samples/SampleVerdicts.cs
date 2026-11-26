using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>How detection's answer for one sample compares with the approved answer.</summary>
internal enum SampleVerdict
{
    /// <summary>The approved answer was found again.</summary>
    Pass,

    /// <summary>Something other than the approved answer was found.</summary>
    Fail,

    /// <summary>The sample has no approved answer yet.</summary>
    New,

    /// <summary>The sample's file changed since its answer was approved, so the answer may no longer hold.</summary>
    Changed,
}

internal static class SampleVerdicts
{
    /// <summary>
    /// Judges detection's answer against the approved one, each edge within the approved tolerance. "Already cropped"
    /// passes only on already cropped, and "none" only on nothing found.
    /// </summary>
    /// <param name="approved">The approved answer for the sample, or <c>null</c> when there is none.</param>
    /// <param name="fingerprint">The fingerprint of the sample's file as it is now.</param>
    /// <param name="found">What detection found.</param>
    public static SampleVerdict Judge(ApprovedAnswer? approved, string fingerprint, Finding found)
    {
        if (approved is null)
            return SampleVerdict.New;
        if (approved.Fingerprint != fingerprint)
            return SampleVerdict.Changed;
        var matches = approved.AlreadyCropped == found.IsAlreadyCropped
            && Matches(approved.Subject, found.Subject, approved.Tolerance);
        return matches ? SampleVerdict.Pass : SampleVerdict.Fail;
    }

    private static bool Matches(Subject? approved, Subject? found, int tolerance) =>
        approved is null || found is null
            ? approved is null && found is null
            : Matches(approved.Window, found.Window, tolerance)
                && approved.Popups.Count == found.Popups.Count
                && approved.Popups.Zip(found.Popups).All(pair => Matches(pair.First, pair.Second, tolerance));

    private static bool Matches(PixelRect approved, PixelRect found, int tolerance) =>
        Math.Abs(approved.X - found.X) <= tolerance && Math.Abs(approved.Y - found.Y) <= tolerance
        && Math.Abs(approved.Right - found.Right) <= tolerance && Math.Abs(approved.Bottom - found.Bottom) <= tolerance;
}
