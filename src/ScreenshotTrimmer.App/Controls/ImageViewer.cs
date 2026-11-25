using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ScreenshotTrimmer.App.Controls;

/// <summary>Draws one image fitted to the control and centred, never enlarged past 100%.</summary>
public sealed class ImageViewer : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ImageViewer, Bitmap?>(nameof(Source));

    static ImageViewer()
    {
        AffectsRender<ImageViewer>(SourceProperty);
        ClipToBoundsProperty.OverrideDefaultValue<ImageViewer>(true);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (Source is not { } bitmap || Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        var size = bitmap.PixelSize;
        var zoom = Math.Min(1, Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height));
        var shown = new Size(size.Width * zoom, size.Height * zoom);
        var destination = new Rect(
            new Point((Bounds.Width - shown.Width) / 2, (Bounds.Height - shown.Height) / 2), shown);

        // Sharp pixels at 100%, smooth downscaling below it.
        var interpolation = zoom >= 1 ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = interpolation }))
            context.DrawImage(bitmap, new Rect(0, 0, size.Width, size.Height), destination);
    }
}
