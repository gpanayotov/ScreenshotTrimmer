using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The <b>Subject</b> of a screenshot as detection finds it: the window rectangle plus the rectangles of any popups
/// whose <b>Popup overhang</b> sticks out past it, in the image's pixel coordinates.
/// </summary>
public sealed record Subject(PixelRect Window)
{
    /// <summary>The popup rectangles that belong to the window; empty when it has none.</summary>
    public IReadOnlyList<PixelRect> Popups { get; init; } = [];

    /// <summary>The crop Step that keeps this Subject.</summary>
    public Crop ToCrop() => Popups.Count == 0 ? Crop.Classic(Window) : Crop.WithPopups(Window, Popups);

    public bool Equals(Subject? other) =>
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
