using ScreenshotTrimmer.Processing.Detection;

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
    /// <param name="approved">The approved answer for the sample, or <c>null</c> when there is none.</param>
    /// <param name="fingerprint">The fingerprint of the sample's file as it is now.</param>
    /// <param name="found">What detection found, or <c>null</c> for nothing.</param>
    public static SampleVerdict Judge(ApprovedAnswer? approved, string fingerprint, Subject? found)
    {
        if (approved is null)
            return SampleVerdict.New;
        if (approved.Fingerprint != fingerprint)
            return SampleVerdict.Changed;
        return Equals(approved.Subject, found) ? SampleVerdict.Pass : SampleVerdict.Fail;
    }
}
