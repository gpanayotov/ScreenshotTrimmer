namespace ScreenshotTrimmer.Processing.Files;

/// <summary>
/// Orders names the way a file manager does: runs of digits compare as numbers (<c>shot-2</c> before <c>shot-10</c>),
/// other characters compare case-insensitively and culture-invariantly. Names equal under those rules fall back
/// to an ordinal comparison so the order is always deterministic.
/// </summary>
internal sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        int i = 0, j = 0;
        while (i < x.Length && j < y.Length)
        {
            if (char.IsAsciiDigit(x[i]) && char.IsAsciiDigit(y[j]))
            {
                var byNumber = CompareNumbers(x, ref i, y, ref j);
                if (byNumber != 0) return byNumber;
                continue;
            }

            var byChar = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
            if (byChar != 0) return byChar;
            i++;
            j++;
        }

        var byLength = (x.Length - i).CompareTo(y.Length - j);
        return byLength != 0 ? byLength : string.CompareOrdinal(x, y);
    }

    private static int CompareNumbers(string x, ref int i, string y, ref int j)
    {
        var a = ReadDigits(x, ref i).TrimStart('0');
        var b = ReadDigits(y, ref j).TrimStart('0');
        var byMagnitude = a.Length.CompareTo(b.Length);
        return byMagnitude != 0 ? byMagnitude : a.SequenceCompareTo(b);
    }

    private static ReadOnlySpan<char> ReadDigits(string s, ref int index)
    {
        var start = index;
        while (index < s.Length && char.IsAsciiDigit(s[index])) index++;
        return s.AsSpan(start, index - start);
    }
}
