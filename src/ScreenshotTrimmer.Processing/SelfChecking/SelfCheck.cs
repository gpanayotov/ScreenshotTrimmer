using ScreenshotTrimmer.Processing.Detection;
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
    private static readonly PixelRect Dialog = new(100, 80, 400, 250);

    /// <summary>Runs the check in a temporary folder, deleted afterwards, and returns its outcome; it never throws.</summary>
    public static SelfCheckResult Run()
    {
        var stage = "drawing the screenshot";
        string? folder = null;
        try
        {
            var screenshot = DrawScreenshot();

            stage = "creating a temporary folder";
            folder = Directory.CreateTempSubdirectory("ScreenshotTrimmer.SelfCheck-").FullName;

            stage = "encoding and decoding PNG";
            var png = Path.Combine(folder, "screenshot.png");
            WriteFile(png, stream => PngEncoder.Write(screenshot, stream));
            var original = ImageFile.Load(png);
            if (!original.PixelsEqual(screenshot))
                return Fail(stage, "the decoded PNG differs from the drawn screenshot.");

            stage = "encoding and decoding JPEG";
            var jpeg = Path.Combine(folder, "screenshot.jpg");
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
            var saved = Path.Combine(folder, "result.png");
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
        finally
        {
            if (folder is not null)
                TryDelete(folder);
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

    /// <summary>A light dialog with rounded corners, a title, text and a button, over a dimmed application page.</summary>
    private static RgbaImage DrawScreenshot()
    {
        var info = new SKImageInfo(800, 500, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(96, 98, 104));
            Fill(canvas, new SKRect(0, 0, 800, 40), new SKColor(40, 52, 70));
            for (var y = 70; y + 8 < 500; y += 22)
                Fill(canvas, SKRect.Create(30, y, 400, 8), new SKColor(70, 72, 78));

            using var dialog = new SKPaint { Color = new SKColor(250, 250, 250), IsAntialias = true };
            canvas.DrawRoundRect(SKRect.Create(Dialog.X, Dialog.Y, Dialog.Width, Dialog.Height), 8, 8, dialog);
            var ink = new SKColor(41, 41, 41);
            Fill(canvas, SKRect.Create(Dialog.X + 20, Dialog.Y + 18, 160, 12), ink);
            for (var y = Dialog.Y + 50; y + 40 < Dialog.Bottom - 50; y += 18)
                Fill(canvas, SKRect.Create(Dialog.X + 20, y, Dialog.Width * 2 / 3, 7), ink);
            Fill(canvas, SKRect.Create(Dialog.Right - 110, Dialog.Bottom - 44, 90, 28), new SKColor(41, 83, 250));
        }

        var image = new RgbaImage(info.Width, info.Height);
        bitmap.GetPixelSpan().CopyTo(image.Bytes);
        return image;
    }

    private static void Fill(SKCanvas canvas, SKRect rect, SKColor colour)
    {
        using var paint = new SKPaint { Color = colour };
        canvas.DrawRect(rect, paint);
    }

    private static void WriteJpeg(RgbaImage image, Stream destination)
    {
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(image.Bytes.ToArray(), 0, bitmap.GetPixels(), image.Bytes.Length);
        using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 95)
            ?? throw new InvalidOperationException("SkiaSharp could not encode the image as JPEG.");
        data.SaveTo(destination);
    }

    private static void WriteFile(string path, Action<Stream> write)
    {
        using var file = File.Create(path);
        write(file);
    }

    private static void TryDelete(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>The outcome of <see cref="SelfCheck.Run"/>: success, or a description of the first failure.</summary>
public sealed record SelfCheckResult(string? Failure)
{
    public static SelfCheckResult Success { get; } = new((string?)null);

    public bool Passed => Failure is null;
}
