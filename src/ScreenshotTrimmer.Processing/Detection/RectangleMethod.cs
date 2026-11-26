using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Detection;

/// <summary>
/// The rectangle method, for screenshots with no modal backdrop: rectangles are assembled from the image's edges, and the
/// windows are the ones that cast an elevation shadow.
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
    private const int MinimumShadowedSides = 2;

    /// <summary>The shortest run of edge pixels the rectangle method needs in an <see cref="EdgeMap"/>.</summary>
    public const int MinimumRun = MinimumSide - 2 * CornerMargin;

    /// <summary>The rectangles of <paramref name="rectangles"/> that cast an elevation shadow on at least two sides, largest first.</summary>
    public static IReadOnlyList<PixelRect> Windows(RgbaImage image, IReadOnlyList<PixelRect> rectangles) =>
        rectangles
            .Where(rectangle => ElevationShadow.ShadowedSides(image, rectangle, CornerMargin) >= MinimumShadowedSides)
            .OrderByDescending(rectangle => (long)rectangle.Width * rectangle.Height)
            .ThenBy(rectangle => rectangle.Y)
            .ThenBy(rectangle => rectangle.X)
            .ToList();

    /// <summary>
    /// The popups of <paramref name="rectangles"/> whose <b>Popup overhang</b> belongs to <paramref name="window"/>,
    /// top to bottom then left to right. A popup has all four sides visible, or hidden only under a smaller rectangle
    /// lying on top such as its submenu, is smaller than the window so lies on top of it, overlaps the window or another of its popups (a submenu beside its menu), sticks out past the window by at
    /// least <see cref="MinimumOverhang"/> pixels on some side, and casts an elevation shadow on at least two sides.
    /// </summary>
    public static IReadOnlyList<PixelRect> Popups(
        RgbaImage image, EdgeMap edges, IReadOnlyList<PixelRect> rectangles, PixelRect window)
    {
        var area = Area(window);
        var onTop = new Overlay(rectangles, edges.Width, edges.Height);
        var candidates = rectangles
            .Where(rectangle => Area(rectangle) < area
                && Overhang(rectangle, window) >= MinimumOverhang
                && ElevationShadow.ShadowedSides(image, rectangle, CornerMargin) >= MinimumShadowedSides
                && IsOutlined(edges, rectangle, onTop))
            .ToList();
        // Popups attach to the window directly, or through a popup already attached.
        var attached = new List<PixelRect>();
        for (var added = true; added;)
        {
            added = false;
            foreach (var candidate in candidates)
                if (!attached.Contains(candidate)
                    && (Overlaps(candidate, window) || attached.Any(popup => Overlaps(candidate, popup))))
                {
                    attached.Add(candidate);
                    added = true;
                }
        }
        return [.. attached.OrderBy(popup => popup.Y).ThenBy(popup => popup.X)];
    }

    /// <summary>The least a popup sticks out past its window, in pixels.</summary>
    private const int MinimumOverhang = 8;

    /// <summary>How far <paramref name="popup"/> sticks out past <paramref name="window"/> on its furthest side.</summary>
    private static int Overhang(PixelRect popup, PixelRect window) =>
        Math.Max(Math.Max(window.X - popup.X, popup.Right - window.Right), Math.Max(window.Y - popup.Y, popup.Bottom - window.Bottom));

    private static bool Overlaps(PixelRect a, PixelRect b) => !a.Intersect(b).IsEmpty;

    /// <summary>Every rectangle whose four sides are visible, or hidden only under a rectangle lying on top.</summary>
    public static IReadOnlyList<PixelRect> FindRectangles(EdgeMap edges, CancellationToken cancellation)
    {
        var found = Assemble(edges, Overlay.None, near: null, cancellation);
        // Each pass may find a rectangle hidden under one found by the last, such as a menu under its submenu, and
        // then the window under the menu. Only side runs near a rectangle new in the last pass can give anything new.
        var added = found;
        for (var pass = 1; pass < MaximumPasses && added.Count > 0; pass++)
        {
            var near = new Coverage(added, edges.Width, edges.Height);
            var again = Assemble(edges, new Overlay(found, edges.Width, edges.Height), near, cancellation);
            added = again.Where(rectangle => !found.Any(other => Competes(other, rectangle))).ToList();
            found = [.. again, .. found.Where(rectangle => !again.Any(other => Competes(other, rectangle)))];
        }
        return found;
    }

    /// <summary>How many rectangles deep the assembly looks for rectangles hidden under others.</summary>
    private const int MaximumPasses = 3;

    /// <summary>
    /// Whether all four sides of <paramref name="rectangle"/> pass as the edges of a rectangle, hidden only under one of
    /// the <paramref name="rectangles"/> found that lies on top of it.
    /// </summary>
    public static bool IsOutlined(EdgeMap edges, PixelRect rectangle, IReadOnlyList<PixelRect> rectangles) =>
        IsOutlined(edges, rectangle, new Overlay(rectangles, edges.Width, edges.Height));

    private static bool IsOutlined(EdgeMap edges, PixelRect rectangle, Overlay onTop)
    {
        if (rectangle.X < 1 || rectangle.Y < 1 || rectangle.Right >= edges.Width || rectangle.Bottom >= edges.Height)
            return false;
        var (top, bottom) = (rectangle.Y + CornerMargin, rectangle.Bottom - CornerMargin);
        var (left, right) = (rectangle.X + CornerMargin, rectangle.Right - CornerMargin);
        var area = Area(rectangle);
        return Evaluate(edges, onTop, area, vertical: true, rectangle.X, top, bottom) is not null
            && Evaluate(edges, onTop, area, vertical: true, rectangle.Right, top, bottom) is not null
            && Evaluate(edges, onTop, area, vertical: false, rectangle.Y, left, right) is not null
            && Evaluate(edges, onTop, area, vertical: false, rectangle.Bottom, left, right) is not null;
    }

    private static long Area(PixelRect rectangle) => (long)rectangle.Width * rectangle.Height;

    private readonly record struct SideRun(int Column, int Start, int End);

    /// <summary>A rectangle with the evidence that ranks it against competing lines.</summary>
    private readonly record struct Candidate(PixelRect Rectangle, int Evidence, int Distance, int Strength);

    /// <summary>
    /// The rectangles assembled from the edges whose sides are visible, or hidden only under one of
    /// <paramref name="onTop"/> where it lies on top of the rectangle.
    /// </summary>
    /// <param name="near">When given, only side runs whose rectangle could reach one of these are paired.</param>
    private static List<PixelRect> Assemble(EdgeMap edges, Overlay onTop, Coverage? near, CancellationToken cancellation)
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
                    if ((near is null || near.Touches(Reach(left, sides[j])))
                        && BestFor(edges, onTop, left, sides[j]) is { } candidate)
                        candidates.Add(candidate);
            }
        }
        return Distinct(candidates);
    }

    /// <summary>Where the rectangle fitted to a pair of side runs can lie, with its top and bottom searched beyond them.</summary>
    private static PixelRect Reach(SideRun left, SideRun right)
    {
        var top = Math.Min(left.Start, right.Start) - CornerSearch;
        var bottom = Math.Max(left.End, right.End) + 1 + CornerSearch + 1;
        return new PixelRect(left.Column, top, right.Column - left.Column + 1, bottom - top);
    }

    /// <summary>
    /// Which parts of the image a set of rectangles covers, on a coarse grid, to tell quickly whether an area may touch
    /// any of them.
    /// </summary>
    private sealed class Coverage
    {
        private const int Cell = 16;

        // The number of covered cells above and left of each grid point.
        private readonly int[,] _sums;

        public Coverage(IReadOnlyList<PixelRect> rectangles, int width, int height)
        {
            var (columns, rows) = (width / Cell + 1, height / Cell + 1);
            var covered = new bool[columns, rows];
            foreach (var rectangle in rectangles)
                for (var c = CellOf(rectangle.X, columns); c <= CellOf(rectangle.Right, columns); c++)
                    for (var r = CellOf(rectangle.Y, rows); r <= CellOf(rectangle.Bottom, rows); r++)
                        covered[c, r] = true;
            _sums = new int[columns + 1, rows + 1];
            for (var c = 0; c < columns; c++)
                for (var r = 0; r < rows; r++)
                    _sums[c + 1, r + 1] = (covered[c, r] ? 1 : 0) + _sums[c, r + 1] + _sums[c + 1, r] - _sums[c, r];
        }

        /// <summary>Whether <paramref name="area"/> may touch one of the rectangles.</summary>
        public bool Touches(PixelRect area)
        {
            var (columns, rows) = (_sums.GetLength(0) - 1, _sums.GetLength(1) - 1);
            var (left, right) = (CellOf(area.X, columns), CellOf(area.Right, columns) + 1);
            var (top, bottom) = (CellOf(area.Y, rows), CellOf(area.Bottom, rows) + 1);
            return _sums[right, bottom] - _sums[left, bottom] - _sums[right, top] + _sums[left, top] > 0;
        }

        private static int CellOf(int pixel, int cells) => Math.Clamp(pixel / Cell, 0, cells - 1);
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
        var end = Math.Max(left.End, right.End);
        // The top and bottom are fitted before the rectangle's height is known: hidden under a rectangle smaller than
        // the largest the pair could make, and checked again once it is known.
        var (highest, lowest) = (start - CornerSearch, end + 1 + CornerSearch);
        var tops = new List<Line>();
        for (var t = Math.Max(1, highest); t <= Math.Max(left.Start, right.Start) + 1 && t < edges.Height; t++)
            if (Evaluate(edges, onTop, (long)(r - l) * (lowest - t), vertical: false, t, from, to) is { } side)
                tops.Add(new Line(t, side, Math.Abs(start - t) / DistanceStep));
        if (tops.Count == 0)
            return null;

        var bottoms = new List<Line>();
        for (var b = Math.Max(1, Math.Min(left.End, right.End) + 1 - DistanceStep); b <= end + 1 + CornerSearch && b < edges.Height; b++)
        {
            // A side edge can run on a few pixels past the bottom, where a crisp shadow line continues it.
            var nearEnd = b - 1 - left.End is >= -DistanceStep and <= CornerSearch
                || b - 1 - right.End is >= -DistanceStep and <= CornerSearch;
            if (nearEnd && Evaluate(edges, onTop, (long)(r - l) * (b - highest), vertical: false, b, from, to) is { } side)
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
                var rectangle = new PixelRect(l, t, r - l, b - t);
                var area = Area(rectangle);
                if (Evaluate(edges, onTop, area, vertical: true, l, t + CornerMargin, b - CornerMargin) is not { } leftSide
                    || Evaluate(edges, onTop, area, vertical: true, r, t + CornerMargin, b - CornerMargin) is not { } rightSide)
                    continue;
                var topSide = top.Side.PartlyHidden ? Evaluate(edges, onTop, area, vertical: false, t, from, to) : top.Side;
                var bottomSide = bottom.Side.PartlyHidden ? Evaluate(edges, onTop, area, vertical: false, b, from, to) : bottom.Side;
                if (topSide is null || bottomSide is null)
                    continue;
                Side[] all = [topSide.Value, bottomSide.Value, leftSide, rightSide];
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

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> are competing lines for the same rectangle.</summary>
    public static bool Competes(PixelRect a, PixelRect b) =>
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

    /// <summary>The fewest pixels of a side partly hidden under a rectangle on top that must show.</summary>
    private const int MinimumVisible = 4;

    /// <summary>
    /// The side along boundary <paramref name="line"/> from pixel <paramref name="from"/> to <paramref name="to"/>
    /// (exclusive, corners already left out), or <c>null</c> when it does not pass. Parts inside a rectangle of
    /// <paramref name="onTop"/> smaller than <paramref name="area"/>, which lies on top of the candidate rather than
    /// behind it, are hidden and left out.
    /// </summary>
    private static Side? Evaluate(EdgeMap edges, Overlay onTop, long area, bool vertical, int line, int from, int to)
    {
        if (to - from < MinimumRun)
            return null;
        if (edges.Covers(vertical, line, from, to))
            return Measure(edges, [(from, to)], partlyHidden: false, vertical, line);
        if (onTop.VisibleParts(vertical, line, from, to, area) is not { } visible)
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
        // A side mostly hidden under a rectangle on top, such as a dropdown nearly as wide as its dialog, may show only
        // a few pixels at each end.
        if (visible < (partlyHidden ? MinimumVisible : MinimumRun) || count * 100 < visible * CoveragePercent)
            return null;
        return new Side(count, visible, stepSum, partlyHidden);
    }

    /// <summary>
    /// Rectangles that may lie on top of others, indexed by line: along each boundary, the spans hidden under the
    /// rectangles whose inside the boundary crosses. A rectangle a side crosses sticks out past the candidate on that side.
    /// </summary>
    private sealed class Overlay
    {
        public static readonly Overlay None = new([], 1, 1);

        /// <summary>The lines in one bucket of the index of rectangles by the lines they cross.</summary>
        private const int BucketSize = 64;

        private readonly IReadOnlyList<PixelRect> _rectangles;
        private readonly List<int>?[] _columnBuckets;
        private readonly List<int>?[] _rowBuckets;
        // Each line's spans, built when first asked for; an empty array for a line no rectangle crosses.
        private readonly Span[]?[] _byColumn;
        private readonly Span[]?[] _byRow;

        /// <summary>
        /// Pixels <see cref="From"/> to <see cref="To"/> along a line, hidden under a rectangle of <see cref="Area"/>;
        /// <see cref="ReachesTo"/> is the furthest any span up to this one in the line's order reaches, for searching.
        /// </summary>
        private readonly record struct Span(int From, int To, long Area, int ReachesTo);

        public Overlay(IReadOnlyList<PixelRect> rectangles, int width, int height)
        {
            _rectangles = rectangles;
            _columnBuckets = new List<int>?[width / BucketSize + 1];
            _rowBuckets = new List<int>?[height / BucketSize + 1];
            for (var i = 0; i < rectangles.Count; i++)
            {
                var rectangle = rectangles[i];
                // The lines crossing a rectangle's inside are X + 1 to Right - 1, and Y + 1 to Bottom - 1.
                for (var bucket = Bucket(rectangle.X + 1, width); bucket <= Bucket(rectangle.Right - 1, width); bucket++)
                    (_columnBuckets[bucket] ??= []).Add(i);
                for (var bucket = Bucket(rectangle.Y + 1, height); bucket <= Bucket(rectangle.Bottom - 1, height); bucket++)
                    (_rowBuckets[bucket] ??= []).Add(i);
            }
            _byColumn = new Span[]?[width + 1];
            _byRow = new Span[]?[height + 1];
        }

        private static int Bucket(int line, int size) => Math.Clamp(line, 0, size) / BucketSize;

        /// <summary>The spans hidden along a line, sorted, from the rectangles whose inside it crosses.</summary>
        private Span[] SpansAt(bool vertical, int line)
        {
            var lines = vertical ? _byColumn : _byRow;
            if (lines[line] is { } built)
                return built;
            var buckets = vertical ? _columnBuckets : _rowBuckets;
            var spans = new List<Span>();
            if (buckets[line / BucketSize] is { } candidates)
                foreach (var i in candidates)
                {
                    var rectangle = _rectangles[i];
                    if (vertical && line > rectangle.X && line < rectangle.Right)
                        spans.Add(new Span(rectangle.Y - HiddenSlack, rectangle.Bottom + HiddenSlack, Area(rectangle), 0));
                    else if (!vertical && line > rectangle.Y && line < rectangle.Bottom)
                        spans.Add(new Span(rectangle.X - HiddenSlack, rectangle.Right + HiddenSlack, Area(rectangle), 0));
                }
            return lines[line] = Sorted(spans);
        }

        /// <summary>
        /// The parts of pixels <paramref name="from"/> to <paramref name="to"/> along a boundary that no rectangle
        /// smaller than <paramref name="area"/> hides, in order; <c>null</c> when none of it is hidden.
        /// </summary>
        public List<(int From, int To)>? VisibleParts(bool vertical, int line, int from, int to, long area)
        {
            if (line < 0 || line >= (vertical ? _byColumn : _byRow).Length)
                return null;
            var spans = SpansAt(vertical, line);
            // The spans are in order of where they begin: the first that may reach past the side's first pixel.
            int low = 0, high = spans.Length;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (spans[middle].ReachesTo <= from)
                    low = middle + 1;
                else
                    high = middle;
            }
            List<(int From, int To)>? parts = null;
            var next = from;
            for (var i = low; i < spans.Length; i++)
            {
                var span = spans[i];
                if (span.From >= to)
                    break;
                if (span.To <= from || span.Area >= area)
                    continue;
                parts ??= [];
                if (span.From > next)
                    parts.Add((next, span.From));
                next = Math.Max(next, span.To);
            }
            if (parts is null)
                return null;
            if (next < to)
                parts.Add((next, to));
            return parts;
        }

        private static Span[] Sorted(List<Span> spans)
        {
            spans.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From)
                : a.To != b.To ? a.To.CompareTo(b.To)
                : a.Area.CompareTo(b.Area));
            var sorted = spans.ToArray();
            var reach = int.MinValue;
            for (var i = 0; i < sorted.Length; i++)
            {
                reach = Math.Max(reach, sorted[i].To);
                sorted[i] = sorted[i] with { ReachesTo = reach };
            }
            return sorted;
        }
    }

    /// <summary>The pixels beside a rectangle on top that its own edge and shadow may blur on the side below it.</summary>
    private const int HiddenSlack = 2;
}
