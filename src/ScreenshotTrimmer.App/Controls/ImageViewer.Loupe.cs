using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.Processing.Viewing;
using ImageRect = ScreenshotTrimmer.Processing.Imaging.PixelRect;

namespace ScreenshotTrimmer.App.Controls;

/// <summary>
/// The <b>Loupe</b>: while the crop is drawn, dragged or nudged, a floating view of the Original's pixels around the
/// held handle, about eight times larger with sharp square pixels whatever the zoom and the display scaling, with a
/// crosshair on the pixel the edge lands on, the crop edge, and that pixel's coordinates. It sits beside the pointer,
/// or the nudged handle, inside the viewer.
/// </summary>
public sealed partial class ImageViewer
{
    /// <summary>Device-independent units per image pixel in the Loupe, about 8 screen pixels at 100% scaling.</summary>
    private const double LoupeMagnification = 8;

    /// <summary>The height of the coordinates line under the magnified pixels, in device-independent units.</summary>
    private const double LoupeLabelHeight = 20;

    private const double LoupeFontSize = 12;

    /// <summary>How long after the last nudge the Loupe stays, longer than the keyboard's repeat delay.</summary>
    private static readonly TimeSpan NudgeLoupeTime = TimeSpan.FromMilliseconds(800);

    private static readonly IBrush LoupePanel = new ImmutableSolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
    private static readonly IBrush LoupeOutsideImage = new ImmutableSolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
    private static readonly IBrush LoupeDim = new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0, 0, 0));
    private static readonly IPen LoupeBorder = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), 1);
    private static readonly IPen CrosshairLine = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xB0, 0xFF, 0x30, 0x30)), 1);
    private static readonly IPen CrosshairPixel = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xFF, 0x30, 0x30)), 2);

    private DispatcherTimer? _nudgeLoupeTimer;

    /// <summary>Where the pointer is during a drag, so the Loupe can follow it; <c>null</c> until it first moves.</summary>
    private ViewPoint? _editPointer;

    /// <summary>Whether the Loupe is shown for the nudged handle.</summary>
    private bool _isNudgeLoupeShown;

    /// <summary>Shows the Loupe on the nudged handle until the nudging stops.</summary>
    private void ShowNudgeLoupe()
    {
        _nudgeLoupeTimer ??= new DispatcherTimer(NudgeLoupeTime, DispatcherPriority.Normal, (_, _) => HideNudgeLoupe());
        _nudgeLoupeTimer.Stop();
        _nudgeLoupeTimer.Start();
        _isNudgeLoupeShown = true;
        InvalidateVisual();
    }

    private void HideNudgeLoupe()
    {
        _nudgeLoupeTimer?.Stop();
        if (!_isNudgeLoupeShown)
            return;
        _isNudgeLoupeShown = false;
        InvalidateVisual();
    }

    /// <summary>
    /// The Loupe to show now and the view point, in device-independent units, it goes beside: the pointer during a
    /// drag, the nudged handle's pixel while nudging; <c>null</c> otherwise.
    /// </summary>
    private (Loupe Loupe, ViewPoint Anchor)? CurrentLoupe(Viewport viewport)
    {
        var scaling = Scaling;
        if (_editFrom is not null)
        {
            if (_editPointer is not { } pointer || _edited is null)
                return null;
            return Loupe.At(_edited, _editedHandle, pointer, viewport, scaling) is { } dragged ? (dragged, pointer) : null;
        }
        if (!_isNudgeLoupeShown || _selected == CropHandle.None || EditableCrop() is not { } crop
            || Loupe.At(crop, _selected, pointer: null, viewport, scaling) is not { } nudged)
            return null;
        var (x, y) = viewport.ToView(nudged.X + 0.5, nudged.Y + 0.5);
        return (nudged, new ViewPoint(x / scaling, y / scaling));
    }

    /// <summary>Draws the Loupe, if one is shown, from the Original <paramref name="bitmap"/>.</summary>
    private void DrawLoupe(DrawingContext context, Bitmap bitmap, Viewport viewport, ImageRect? crop)
    {
        if (CurrentLoupe(viewport) is not var (loupe, anchor))
            return;

        // Each image pixel is a whole number of physical pixels, so all are the same size at any scaling.
        var scaling = Scaling;
        var cell = Math.Max(1, Math.Round(LoupeMagnification * scaling)) / scaling;
        var side = Loupe.Span * cell;
        var at = Loupe.Place(anchor, side, side + LoupeLabelHeight, Bounds.Width, Bounds.Height);
        var left = Math.Round(at.X * scaling) / scaling;
        var top = Math.Round(at.Y * scaling) / scaling;
        var pixels = new Rect(left, top, side, side);
        var area = loupe.Area;

        // Image pixel (x, y) is drawn at this point in the Loupe.
        Point ToLoupe(double x, double y) => new(left + (x - area.X) * cell, top + (y - area.Y) * cell);

        context.FillRectangle(LoupePanel, new Rect(left, top, side, side + LoupeLabelHeight));
        using (context.PushClip(pixels))
        {
            context.FillRectangle(LoupeOutsideImage, pixels);
            var shown = area.Intersect(new ImageRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height));
            if (!shown.IsEmpty)
            {
                var destination = new Rect(ToLoupe(shown.X, shown.Y), ToLoupe(shown.Right, shown.Bottom));
                context.FillRectangle(BackgroundKind switch
                {
                    ViewerBackground.White => Brushes.White,
                    ViewerBackground.DarkGrey => DarkGrey,
                    _ => Checkerboard,
                }, destination);
                using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.None }))
                    context.DrawImage(bitmap, new Rect(shown.X, shown.Y, shown.Width, shown.Height), destination);
            }

            if (crop is { } rectangle)
                DrawLoupeCrop(context, pixels, new Rect(ToLoupe(rectangle.X, rectangle.Y), ToLoupe(rectangle.Right, rectangle.Bottom)));

            // The crosshair: lines up to the pixel aimed at, and a square around it, leaving the pixel itself clear.
            var target = new Rect(ToLoupe(loupe.X, loupe.Y), new Size(cell, cell));
            var centre = target.Center;
            context.DrawLine(CrosshairLine, new Point(pixels.Left, centre.Y), new Point(target.Left - 1, centre.Y));
            context.DrawLine(CrosshairLine, new Point(target.Right + 1, centre.Y), new Point(pixels.Right, centre.Y));
            context.DrawLine(CrosshairLine, new Point(centre.X, pixels.Top), new Point(centre.X, target.Top - 1));
            context.DrawLine(CrosshairLine, new Point(centre.X, target.Bottom + 1), new Point(centre.X, pixels.Bottom));
            context.DrawRectangle(null, CrosshairPixel, target.Inflate(1));
        }
        context.DrawRectangle(null, LoupeBorder, new Rect(left, top, side, side + LoupeLabelHeight));

        var label = new FormattedText(
            Localizer.Instance.Format("LoupePosition", loupe.X, loupe.Y),
            Localizer.Instance.Culture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            LoupeFontSize,
            Brushes.White);
        context.DrawText(label, new Point(
            left + (side - label.Width) / 2,
            top + side + (LoupeLabelHeight - label.Height) / 2));
    }

    /// <summary>Dims the magnified pixels outside the crop <paramref name="inside"/> and outlines it, within <paramref name="pixels"/>.</summary>
    private static void DrawLoupeCrop(DrawingContext context, Rect pixels, Rect inside)
    {
        context.FillRectangle(LoupeDim, new Rect(pixels.Left, pixels.Top, pixels.Width, Math.Max(0, inside.Top - pixels.Top)));
        context.FillRectangle(LoupeDim, new Rect(pixels.Left, inside.Bottom, pixels.Width, Math.Max(0, pixels.Bottom - inside.Bottom)));
        var top = Math.Max(inside.Top, pixels.Top);
        var height = Math.Max(0, Math.Min(inside.Bottom, pixels.Bottom) - top);
        context.FillRectangle(LoupeDim, new Rect(pixels.Left, top, Math.Max(0, inside.Left - pixels.Left), height));
        context.FillRectangle(LoupeDim, new Rect(inside.Right, top, Math.Max(0, pixels.Right - inside.Right), height));
        context.DrawRectangle(null, CropOutlineShade, inside);
        context.DrawRectangle(null, CropOutline, inside);
    }
}
