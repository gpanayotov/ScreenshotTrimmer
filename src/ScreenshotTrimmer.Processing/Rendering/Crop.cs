using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Rendering;

/// <summary>
/// The crop Step: the window rectangle to keep, plus any popup rectangles whose <b>Popup overhang</b>
/// sticks out past it, in the Original's pixel coordinates.
/// The same crop whether it was drawn by hand (Classic crop) or found by Auto crop.
/// Rectangles are clipped to the image. The Result covers the bounding rectangle of all of them,
/// and the pixels in it that lie outside every rectangle are fully transparent.
/// </summary>
public sealed record Crop(PixelRect Window)
{
    /// <summary>The popup rectangles kept together with the window; empty for a Classic crop.</summary>
    public IReadOnlyList<PixelRect> Popups { get; init; } = [];

    /// <summary>A Classic crop: exactly the rectangle the user drew, with no popups.</summary>
    public static Crop Classic(PixelRect rectangle) => new(rectangle);

    /// <summary>A crop that keeps the window together with the given popups.</summary>
    public static Crop WithPopups(PixelRect window, params IEnumerable<PixelRect> popups) =>
        new(window) { Popups = [.. popups] };

    public bool Equals(Crop? other) =>
        other is not null && Window == other.Window && Popups.SequenceEqual(other.Popups);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Window);
        foreach (var popup in Popups)
            hash.Add(popup);
        return hash.ToHashCode();
    }
}
