namespace ScreenshotTrimmer.App;

/// <summary>What a newly drawn rectangle does. Adjusting an existing crop's handles works the same with either.</summary>
public enum CropTool
{
    /// <summary>The rectangle becomes a <b>Classic crop</b>.</summary>
    Classic,

    /// <summary><b>Auto crop in region</b>: the crop snaps to the largest Subject inside the rectangle.</summary>
    AutoCropInRegion,
}
