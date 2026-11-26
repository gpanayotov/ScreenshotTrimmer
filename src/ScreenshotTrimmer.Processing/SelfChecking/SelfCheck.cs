using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.SelfChecking;

/// <summary>
/// Runs the real image path with no interface, on a screenshot it draws itself: encoding and decoding PNG and JPEG,
/// Auto crop's Subject detection, rendering with Clean edges and Shadow, and encoding the Result. It proves that the
/// native imaging libraries load, so a packaged build that lost them fails it.
/// </summary>
public static class SelfCheck
{
    /// <summary>Where the drawn screenshot's dialog lies, and so about where the Subject Auto crop finds must lie.</summary>
    private static readonly PixelRect Dialog = SyntheticScreenshot.DefaultDialog;

    /// <summary>Runs the check in a temporary folder, deleted afterwards, and returns its outcome; it never throws.</summary>
    public static SelfCheckResult Run()
    {
        var stage = "drawing the screenshot";
        try
        {
            var screenshot = SyntheticScreenshot.DialogOverDimmedApplication();

            stage = "creating a temporary folder";
            using var folder = new TemporaryFolder("ScreenshotTrimmer.SelfCheck-");

            stage = "encoding and decoding PNG";
            var png = folder.File("screenshot.png");
            WriteFile(png, stream => PngEncoder.Write(screenshot, stream));
            var original = ImageFile.Load(png);
            if (!original.PixelsEqual(screenshot))
                return Fail(stage, "the decoded PNG differs from the drawn screenshot.");

            stage = "encoding and decoding JPEG";
            var jpeg = folder.File("screenshot.jpg");
            WriteFile(jpeg, stream => WriteJpeg(screenshot, stream));
            var fromJpeg = ImageFile.Load(jpeg);
            if (fromJpeg.Width != screenshot.Width || fromJpeg.Height != screenshot.Height)
                return Fail(stage, $"the decoded JPEG is {fromJpeg.Width}x{fromJpeg.Height}, not {screenshot.Width}x{screenshot.Height}.");
            var (x, y) = (Dialog.X + Dialog.Width / 2, Dialog.Y + Dialog.Height / 2);
            if (fromJpeg[x, y].DifferenceTo(screenshot[x, y]) > 40)
                return Fail(stage, "the decoded JPEG's colours are far from the drawn screenshot's.");

            stage = "finding the Subject";
            var subject = SubjectDetection.Find(original);
            if (subject is null)
                return Fail(stage, "no Subject was found.");
            if (!Near(subject.Window, Dialog))
                return Fail(stage, $"the Subject is {subject.Window}, not the dialog at {Dialog}.");

            stage = "rendering with Clean edges and Shadow";
            var steps = new Steps
            {
                Crop = subject.ToCrop(),
                FrameLines = true,
                RoundedCorners = new RoundedCorners(),
                Shadow = new Shadow(),
            };
            var (result, report) = Renderer.Render(original, steps);
            if (report.ShadowPadding <= 0)
                return Fail(stage, "the Shadow added no padding.");
            var padded = new PixelRect(0, 0, Dialog.Width + 2 * report.ShadowPadding, Dialog.Height + 2 * report.ShadowPadding);
            if (!Near(new PixelRect(0, 0, result.Width, result.Height), padded))
                return Fail(stage, $"the Result is {result.Width}x{result.Height}, not about {padded.Width}x{padded.Height}.");
            if (result[0, 0].A != 0)
                return Fail(stage, "the Result's corner is not transparent.");

            stage = "encoding and decoding the Result";
            var saved = folder.File("result.png");
            WriteFile(saved, stream => PngEncoder.Write(result, stream));
            if (!ImageFile.Load(saved).PixelsEqual(result))
                return Fail(stage, "the decoded Result differs from the rendered one.");

            return SelfCheckResult.Success;
        }
        catch (Exception e)
        {
            // The point of the check is to report any failure, native library loading included, as text.
            return Fail(stage, $"{e.GetType().Name}: {e.Message}{InnerMessage(e)}");
        }
    }

    /// <summary>
    /// Whether every edge of <paramref name="found"/> lies within a few pixels of <paramref name="drawn"/>'s. Detection
    /// may settle a pixel or two inside an antialiased rounded corner; the check is that the image path works, not
    /// where exactly detection draws the line.
    /// </summary>
    private static bool Near(PixelRect found, PixelRect drawn)
    {
        const int tolerance = 4;
        return Math.Abs(found.X - drawn.X) <= tolerance && Math.Abs(found.Y - drawn.Y) <= tolerance
            && Math.Abs(found.Right - drawn.Right) <= tolerance && Math.Abs(found.Bottom - drawn.Bottom) <= tolerance;
    }

    private static SelfCheckResult Fail(string stage, string reason) => new($"Failed while {stage}: {reason}");

    private static string InnerMessage(Exception e) =>
        e.InnerException is { } inner ? $" ({inner.GetType().Name}: {inner.Message})" : "";

    private static void WriteJpeg(RgbaImage image, Stream destination)
    {
        using var bitmap = SyntheticScreenshot.ToBitmap(image);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95)
            ?? throw new InvalidOperationException("SkiaSharp could not encode the image as JPEG.");
        data.SaveTo(destination);
    }

    private static void WriteFile(string path, Action<Stream> write)
    {
        using var file = File.Create(path);
        write(file);
    }
}

/// <summary>The outcome of <see cref="SelfCheck.Run"/>: success, or a description of the first failure.</summary>
public sealed record SelfCheckResult(string? Failure)
{
    public static SelfCheckResult Success { get; } = new((string?)null);

    public bool Passed => Failure is null;
}
