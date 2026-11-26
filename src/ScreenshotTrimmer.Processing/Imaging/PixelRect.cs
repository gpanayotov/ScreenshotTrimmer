namespace ScreenshotTrimmer.Processing.Imaging;

/// <summary>A rectangle of whole pixels in image coordinates, from its top-left corner.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>The part of this rectangle inside <paramref name="other"/>; empty when they do not overlap.</summary>
    public PixelRect Intersect(PixelRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return new PixelRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }

    /// <summary>Whether <paramref name="inner"/> lies wholly inside this rectangle; its sides may lie on this one's.</summary>
    public bool Contains(PixelRect inner) =>
        inner.X >= X && inner.Y >= Y && inner.Right <= Right && inner.Bottom <= Bottom;

    /// <summary>The smallest rectangle that contains both this rectangle and <paramref name="other"/>.</summary>
    public PixelRect Union(PixelRect other)
    {
        var left = Math.Min(X, other.X);
        var top = Math.Min(Y, other.Y);
        return new PixelRect(left, top, Math.Max(Right, other.Right) - left, Math.Max(Bottom, other.Bottom) - top);
    }

    /// <summary>This rectangle in the coordinates of <paramref name="outer"/>, whose top-left corner becomes (0, 0).</summary>
    public PixelRect RelativeTo(PixelRect outer) => this with { X = X - outer.X, Y = Y - outer.Y };
}
