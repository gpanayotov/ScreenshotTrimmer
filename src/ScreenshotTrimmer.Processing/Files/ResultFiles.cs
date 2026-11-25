using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Saves Results to disk under the save rules. Every Result is a PNG and every write is atomic.</summary>
public static class ResultFiles
{
    /// <summary>
    /// Suffix mode: writes <paramref name="result"/> as <c>name&lt;suffix&gt;.png</c> beside the Original,
    /// replacing an earlier Result. The Original is left untouched.
    /// </summary>
    public static SaveReport Save(RgbaImage result, string originalPath, SaveOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);

        var fullOriginal = Path.GetFullPath(originalPath);
        var resultPath = Path.Combine(
            Path.GetDirectoryName(fullOriginal)!,
            Path.GetFileNameWithoutExtension(fullOriginal) + options.ResultSuffix + ".png");

        AtomicFile.Write(resultPath, stream => PngEncoder.Write(result, stream));
        return new SaveReport { Written = [resultPath] };
    }
}
