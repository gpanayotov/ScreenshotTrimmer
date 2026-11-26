using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The rectangle method, for screenshots with no modal backdrop: rectangles are assembled from the image's edges, and the
/// window is the largest one that casts an elevation shadow.
/// </summary>
/// <remarks>
/// <para>
/// Each long vertical run of edge pixels is paired with the runs to its right that start near the same row, through an
/// index of runs by their first row, so no pair of columns is tried blindly. The top of each pair is fitted from the rows
/// near where its runs start, and the bottom from the rows near where they end.
/// </para>
/// <para>
/// A side passes when at least <see cref="CoveragePercent"/>% of it is edge pixels and no gap is longer than
/// <see cref="EdgeMap.MaxGap"/>, ignoring <see cref="CornerMargin"/> pixels at each end for a rounded corner. A side may
/// be partly hidden only under a rectangle that lies on top of the candidate: one with all four sides visible that is
/// smaller, overlaps it and sticks out past it, as an open menu does.
/// </para>
/// <para>
/// Among candidates whose sides are within a few pixels of each other, which are competing lines for the same
/// rectangle, the one with the most edge evidence on its four sides wins; then the one whose top and bottom are nearest
/// where its side edges start and end, in 4px steps; then the one with the stronger colour step. The last rule keeps an
/// edge from moving onto a crisp shadow line 1–2px beside it.
/// </para>
/// </remarks>
internal static class RectangleMethod
{
    /// <summary>The pixels at each end of a side that may be lost to a rounded corner.</summary>
    public const int CornerMargin = 8;

    /// <summary>The share of a side, in percent, that must be edge pixels.</summary>
    private const int CoveragePercent = 85;

    /// <summary>The shortest side of a rectangle, in pixels.</summary>
    private const int MinimumSide = 32;

    /// <summary>How far a top or bottom may lie from where the side edges start or end, as a rounded corner rounds them off.</summary>
    private const int CornerSearch = 16;

    /// <summary>How far apart, in rows, the left and right side edges of one rectangle may start.</summary>
    private const int StartTolerance = 6;

    /// <summary>Candidates whose sides all lie within this many pixels of each other compete for the same rectangle.</summary>
    private const int CompetingDistance = 3;

    /// <summary>The steps, in pixels, in which the distance to where the side edges start is compared.</summary>
    private const int DistanceStep = 4;

    /// <summary>The fewest sides on which the window must cast an elevation shadow.</summary>
    private const int ShadowedSides = 2;

    /// <summary>The shortest run of edge pixels the rectangle method needs in an <see cref="EdgeMap"/>.</summary>
    public const int MinimumRun = MinimumSide - 2 * CornerMargin;

    /// <summary>The rectangles of <paramref name="rectangles"/> that cast an elevation shadow on at least two sides, largest first.</summary>
    public static IReadOnlyList<PixelRect> Windows(RgbaImage image, IReadOnlyList<PixelRect> rectangles) =>
        rectangles
            .Where(rectangle => ElevationShadow.ShadowedSides(image, rectangle, CornerMargin) >= ShadowedSides)
            .OrderByDescending(rectangle => (long)rectangle.Width * rectangle.Height)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.X)
            .ToList();

    /// <summary>Every rectangle whose four sides are visible, or hidden only under a rectangle lying on top.</summary>
    public static IReadOnlyList<PixelRect> FindRectangles(EdgeMap edges, CancellationToken cancellation)
    {
        var visible = Assemble(edges, Overlay.None, cancellation);
        if (visible.Count == 0)
            return visible;
        var all = Assemble(edges, new Overlay(visible, edges.Width, edges.Height), cancellation);
        return [.. all, .. visible.Where(rectangle => !all.Any(other => Competes(other, rectangle)))];
    }

    /// <summary>
    /// Whether all four sides of <paramref name="rectangle"/> pass as the edges of a rectangle, hidden only under one of
    /// the <paramref name="rectangles"/> found that lies on top of it.
    /// </summary>
    public static bool IsOutlined(EdgeMap edges, PixelRect rectangle, IReadOnlyList<PixelRect> rectangles)
    {
        if (rectangle.X < 1 || rectangle.Y < 1 || rectangle.Right >= edges.Width || rectangle.Bottom >= edges.Height)
            return false;
        var onTop = new Overlay(rectangles, edges.Width, edges.Height);
        var (top, bottom) = (rectangle.Y + CornerMargin, rectangle.Bottom - CornerMargin);
        var (left, right) = (rectangle.X + CornerMargin, rectangle.Right - CornerMargin);
        Side?[] sides =
        [
            Evaluate(edges, onTop, vertical: true, rectangle.X, top, bottom),
            Evaluate(edges, onTop, vertical: true, rectangle.Right, top, bottom),
            Evaluate(edges, onTop, vertical: false, rectangle.Y, left, right),
            Evaluate(edges, onTop, vertical: false, rectangle.Bottom, left, right),
        ];
        return sides.All(side => side is not null)
            && (!sides.Any(side => side!.Value.PartlyHidden) || HiddenOnlyUnderSmaller(rectangle, onTop));
    }

    private readonly record struct SideRun(int Column, int Start, int End);

    /// <summary>A rectangle with the evidence that ranks it against competing lines.</summary>
    private readonly record struct Candidate(PixelRect Rectangle, int Evidence, int Distance, int Strength);

    /// <summary>
    /// The rectangles assembled from the edges whose sides are visible, or hidden only under one of
    /// <paramref name="onTop"/> where it lies on top of the rectangle.
    /// </summary>
    private static List<PixelRect> Assemble(EdgeMap edges, Overlay onTop, CancellationToken cancellation)
    {
        var sides = new List<SideRun>();
        for (var x = 1; x < edges.Width; x++)
            foreach (var run in edges.ColumnRunsAt(x))
                sides.Add(new SideRun(x, run.Start, run.End));
        sides.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Column.CompareTo(b.Column));
        // The index of the first side run starting at each row.
        var firstByStart = new int[edges.Height + 1];
        for (int row = 0, i = 0; row <= edges.Height; row++)
        {
            while (i < sides.Count && sides[i].Start < row)
                i++;
            firstByStart[row] = i;
        }

        var candidates = new List<Candidate>();
        for (var i = 0; i < sides.Count; i++)
        {
            if (i % 256 == 0)
                cancellation.ThrowIfCancellationRequested();
            var left = sides[i];
            var fromRow = Math.Max(0, left.Start - StartTolerance);
            var toRow = Math.Min(edges.Height, left.Start + StartTolerance + 1);
            for (var row = fromRow; row < toRow; row++)
            {
                var first = FirstAtOrRightOf(sides, firstByStart[row], firstByStart[row + 1], left.Column + MinimumSide);
                for (var j = first; j < firstByStart[row + 1]; j++)
                    if (BestFor(edges, onTop, left, sides[j]) is { } candidate)
                        candidates.Add(candidate);
            }
        }
        return Distinct(candidates);
    }

    /// <summary>The first side run in <c>[from, to)</c> whose column is at least <paramref name="column"/>.</summary>
    private static int FirstAtOrRightOf(List<SideRun> sides, int from, int to, int column)
    {
        while (from < to)
        {
            var middle = (from + to) / 2;
            if (sides[middle].Column < column)
                from = middle + 1;
            else
                to = middle;
        }
        return from;
    }

    /// <summary>The best rectangle with these left and right side runs, or <c>null</c> when no top and bottom fit.</summary>
    private static Candidate? BestFor(EdgeMap edges, Overlay onTop, SideRun left, SideRun right)
    {
        var (l, r) = (left.Column, right.Column);
        var (from, to) = (l + CornerMargin, r - CornerMargin);
        var start = Math.Min(left.Start, right.Start);
        var tops = new List<Line>();
        for (var t = Math.Max(1, start - CornerSearch); t <= Math.Max(left.Start, right.Start) + 1 && t < edges.Height; t++)
            if (Evaluate(edges, onTop, vertical: false, t, from, to) is { } side)
                tops.Add(new Line(t, side, Math.Abs(start - t) / DistanceStep));
        if (tops.Count == 0)
            return null;

        var end = Math.Max(left.End, right.End);
        var bottoms = new List<Line>();
        for (var b = Math.Max(1, Math.Min(left.End, right.End)); b <= end + 1 + CornerSearch && b < edges.Height; b++)
        {
            var nearEnd = b - 1 - left.End is >= -1 and <= CornerSearch || b - 1 - right.End is >= -1 and <= CornerSearch;
            if (nearEnd && Evaluate(edges, onTop, vertical: false, b, from, to) is { } side)
                bottoms.Add(new Line(b, side, Math.Abs(b - 1 - end) / DistanceStep));
        }
        if (bottoms.Count == 0)
            return null;

        // The best ranked top and bottom between which both sides pass.
        tops.Sort(Line.Ranks);
        bottoms.Sort(Line.Ranks);
        foreach (var top in tops)
            foreach (var bottom in bottoms)
            {
                var (t, b) = (top.Position, bottom.Position);
                if (b - t < MinimumSide)
                    continue;
                if (Evaluate(edges, onTop, vertical: true, l, t + CornerMargin, b - CornerMargin) is not { } leftSide
                    || Evaluate(edges, onTop, vertical: true, r, t + CornerMargin, b - CornerMargin) is not { } rightSide)
                    continue;
                var rectangle = new PixelRect(l, t, r - l, b - t);
                Side[] all = [top.Side, bottom.Side, leftSide, rightSide];
                if (all.Any(side => side.PartlyHidden) && !HiddenOnlyUnderSmaller(rectangle, onTop))
                    continue;
                var count = all.Sum(side => side.Count);
                return new Candidate(
                    rectangle,
                    all.Sum(side => side.PerMille),
                    top.Distance + bottom.Distance,
                    count == 0 ? 0 : all.Sum(side => side.StepSum) / count);
            }
        return null;
    }

    /// <summary>A top or bottom line fitted to a pair of side runs, with its distance from where they start or end, in steps.</summary>
    private readonly record struct Line(int Position, Side Side, int Distance)
    {
        /// <summary>Negative when <paramref name="a"/> ranks above <paramref name="b"/>.</summary>
        public static int Ranks(Line a, Line b) =>
            a.Side.PerMille != b.Side.PerMille ? b.Side.PerMille.CompareTo(a.Side.PerMille)
            : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Side.Strength != b.Side.Strength ? b.Side.Strength.CompareTo(a.Side.Strength)
            : a.Position.CompareTo(b.Position);
    }

    /// <summary>Negative when <paramref name="a"/> ranks above <paramref name="b"/> as the line for the same rectangle.</summary>
    private static int Ranks(Candidate a, Candidate b)
    {
        if (a.Evidence != b.Evidence)
            return b.Evidence.CompareTo(a.Evidence);
        if (a.Distance != b.Distance)
            return a.Distance.CompareTo(b.Distance);
        return b.Strength.CompareTo(a.Strength);
    }

    /// <summary>The candidates without their competing lines: of each group within a few pixels, the best ranked.</summary>
    private static List<PixelRect> Distinct(List<Candidate> candidates)
    {
        candidates.Sort((a, b) =>
        {
            var rank = Ranks(a, b);
            if (rank != 0)
                return rank;
            var (ra, rb) = (a.Rectangle, b.Rectangle);
            return ra.Y != rb.Y ? ra.Y.CompareTo(rb.Y)
                : ra.X != rb.X ? ra.X.CompareTo(rb.X)
                : ra.Bottom != rb.Bottom ? ra.Bottom.CompareTo(rb.Bottom)
                : ra.Right.CompareTo(rb.Right);
        });
        var kept = new List<PixelRect>();
        foreach (var candidate in candidates)
            if (!kept.Any(other => Competes(other, candidate.Rectangle)))
                kept.Add(candidate.Rectangle);
        return kept;
    }

    private static bool Competes(PixelRect a, PixelRect b) =>
        Math.Abs(a.X - b.X) <= CompetingDistance && Math.Abs(a.Y - b.Y) <= CompetingDistance
        && Math.Abs(a.Right - b.Right) <= CompetingDistance && Math.Abs(a.Bottom - b.Bottom) <= CompetingDistance;

    /// <summary>The edge evidence along one side.</summary>
    private readonly record struct Side(int Count, int Visible, int StepSum, bool PartlyHidden)
    {
        /// <summary>The share of the visible side that is edge pixels, in thousandths.</summary>
        public int PerMille => Count * 1000 / Visible;

        /// <summary>The mean colour step across the side's edge pixels.</summary>
        public int Strength => Count == 0 ? 0 : StepSum / Count;
    }

    /// <summary>
    /// The side along boundary <paramref name="line"/> from pixel <paramref name="from"/> to <paramref name="to"/>
    /// (exclusive, corners already left out), or <c>null</c> when it does not pass. Parts inside a rectangle of
    /// <paramref name="onTop"/> are hidden and left out.
    /// </summary>
    private static Side? Evaluate(EdgeMap edges, Overlay onTop, bool vertical, int line, int from, int to)
    {
        if (to - from < MinimumRun)
            return null;
        if (edges.Covers(vertical, line, from, to))
            return Measure(edges, [(from, to)], partlyHidden: false, vertical, line);
        if (onTop.VisibleParts(vertical, line, from, to) is not { } visible)
            return null;
        // Each visible part long enough to be indexed must lie within one run before its pixels are counted.
        foreach (var (partFrom, partTo) in visible)
            if (partTo - partFrom >= MinimumRun && !edges.Covers(vertical, line, partFrom, partTo))
                return null;
        return Measure(edges, visible, partlyHidden: true, vertical, line);
    }

    /// <summary>The edge evidence along the <paramref name="parts"/> of a side, or <c>null</c> when it does not pass.</summary>
    private static Side? Measure(EdgeMap edges, List<(int From, int To)> parts, bool partlyHidden, bool vertical, int line)
    {
        int count = 0, visible = 0, stepSum = 0;
        foreach (var (from, to) in parts)
        {
            // A gap is measured within one visible part: the side may resume from under a rectangle on top.
            var gap = 0;
            for (var p = from; p < to; p++)
            {
                visible++;
                var step = vertical ? edges.VerticalStep(line, p) : edges.HorizontalStep(p, line);
                if (step >= EdgeMap.EdgeStep)
                {
                    count++;
                    stepSum += step;
                    gap = 0;
                }
                else if (++gap > EdgeMap.MaxGap)
                    return null;
            }
        }
        if (visible < MinimumRun || count * 100 < visible * CoveragePercent)
            return null;
        return new Side(count, visible, stepSum, partlyHidden);
    }

    /// <summary>
    /// Rectangles that may lie on top of others, indexed by line: along each boundary, the merged spans hidden under the
    /// rectangles whose inside the boundary crosses. A rectangle a side crosses sticks out past the candidate on that side.
    /// </summary>
    private sealed class Overlay
    {
        public static readonly Overlay None = new([], 1, 1);

        private readonly (int From, int To)[]?[] _byColumn;
        private readonly (int From, int To)[]?[] _byRow;

        public Overlay(IReadOnlyList<PixelRect> rectangles, int width, int height)
        {
            Rectangles = rectangles;
            var columns = new List<(int From, int To)>?[width + 1];
            var rows = new List<(int From, int To)>?[height + 1];
            foreach (var rectangle in rectangles)
            {
                for (var x = rectangle.X + 1; x < rectangle.Right; x++)
                    (columns[x] ??= []).Add((rectangle.Y - HiddenSlack, rectangle.Bottom + HiddenSlack));
                for (var y = rectangle.Y + 1; y < rectangle.Bottom; y++)
                    (rows[y] ??= []).Add((rectangle.X - HiddenSlack, rectangle.Right + HiddenSlack));
            }
            _byColumn = Array.ConvertAll(columns, Merge);
            _byRow = Array.ConvertAll(rows, Merge);
        }

        public IReadOnlyList<PixelRect> Rectangles { get; }

        /// <summary>
        /// The parts of pixels <paramref name="from"/> to <paramref name="to"/> along a boundary that no rectangle hides,
        /// in order; <c>null</c> when none of it is hidden.
        /// </summary>
        public List<(int From, int To)>? VisibleParts(bool vertical, int line, int from, int to)
        {
            var lines = vertical ? _byColumn : _byRow;
            if (line < 0 || line >= lines.Length || lines[line] is not { } spans)
                return null;
            // The first hidden span that ends after the side begins.
            int low = 0, high = spans.Length;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (spans[middle].To <= from)
                    low = middle + 1;
                else
                    high = middle;
            }
            if (low == spans.Length || spans[low].From >= to)
                return null;
            var parts = new List<(int From, int To)>();
            var next = from;
            for (var i = low; i < spans.Length && spans[i].From < to; i++)
            {
                if (spans[i].From > next)
                    parts.Add((next, spans[i].From));
                next = Math.Max(next, spans[i].To);
            }
            if (next < to)
                parts.Add((next, to));
            return parts;
        }

        private static (int From, int To)[]? Merge(List<(int From, int To)>? spans)
        {
            if (spans is null)
                return null;
            spans.Sort();
            var merged = new List<(int From, int To)>();
            foreach (var span in spans)
                if (merged.Count > 0 && span.From <= merged[^1].To)
                    merged[^1] = (merged[^1].From, Math.Max(merged[^1].To, span.To));
                else
                    merged.Add(span);
            return [.. merged];
        }
    }

    /// <summary>The pixels beside a rectangle on top that its own edge and shadow may blur on the side below it.</summary>
    private const int HiddenSlack = 2;

    /// <summary>
    /// Whether every rectangle of <paramref name="onTop"/> that hides part of <paramref name="rectangle"/>'s sides is
    /// smaller than it, so lies on top of it rather than behind.
    /// </summary>
    private static bool HiddenOnlyUnderSmaller(PixelRect rectangle, Overlay onTop)
    {
        var area = (long)rectangle.Width * rectangle.Height;
        foreach (var other in onTop.Rectangles)
        {
            if ((long)other.Width * other.Height < area || other == rectangle)
                continue;
            var crossesSide =
                (rectangle.X > other.X && rectangle.X < other.Right || rectangle.Right > other.X && rectangle.Right < other.Right)
                    && other.Y < rectangle.Bottom && other.Bottom > rectangle.Y
                || (rectangle.Y > other.Y && rectangle.Y < other.Bottom || rectangle.Bottom > other.Y && rectangle.Bottom < other.Bottom)
                    && other.X < rectangle.Right && other.Right > rectangle.X;
            if (crossesSide)
                return false;
        }
        return true;
    }
}
