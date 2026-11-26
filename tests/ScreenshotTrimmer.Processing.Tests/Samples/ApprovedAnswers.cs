using System.Globalization;
using System.Text;
using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

/// <summary>
/// The answer the maintainer approved for one sample: the <see cref="Subject"/> detection should find in it, or
/// <c>null</c> for "none" or "already cropped", and the fingerprint of the file it was approved for.
/// </summary>
internal sealed record ApprovedAnswer(string Fingerprint, Subject? Subject)
{
    /// <summary>The sample is already cropped to its Subject, which is not the same answer as "none".</summary>
    public bool AlreadyCropped { get; init; }

    /// <summary>How many pixels each edge of the window and popups may be off and still pass, where an edge is ambiguous.</summary>
    public int Tolerance { get; init; }

    /// <summary>The sample is a dark-theme screenshot, counted in the dark totals rather than the light ones.</summary>
    public bool Dark { get; init; }

    public static ApprovedAnswer AlreadyCroppedAnswer(string fingerprint) => new(fingerprint, null) { AlreadyCropped = true };
}

/// <summary>
/// Reads and writes the approved-answers file of the local samples check. One line per sample, tab separated:
/// file name, fingerprint, then <c>none</c>, <c>already-cropped</c>, or <c>window x,y,w,h</c> followed by a
/// <c>popup x,y,w,h</c> per popup.
/// Blank lines and lines starting with <c>#</c> are ignored.
/// </summary>
internal static class ApprovedAnswers
{
    private const string None = "none";
    private const string AlreadyCropped = "already-cropped";
    private const string ToleranceFlag = "tolerance";
    private const string DarkFlag = "dark";

    public static Dictionary<string, ApprovedAnswer> Parse(string text)
    {
        var answers = new Dictionary<string, ApprovedAnswer>(StringComparer.Ordinal);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var fields = line.Split('\t');
            if (fields.Length != 3 || TryParseAnswer(fields[1], fields[2], out var answer) is false)
                throw new FormatException($"Approved answers, line {i + 1}: expected \"name<TAB>fingerprint<TAB>answer\", got \"{line}\".");
            answers[fields[0]] = answer;
        }
        return answers;
    }

    public static string Format(IReadOnlyDictionary<string, ApprovedAnswer> answers)
    {
        var text = new StringBuilder();
        text.Append("# Approved answers of the local samples check: file name, fingerprint, then \"none\", \"already-cropped\"\n");
        text.Append("# or the window and popup rectangles (x,y,width,height). Regenerated from detection's output; approve by eye.\n");
        foreach (var (name, answer) in answers.OrderBy(a => a.Key, StringComparer.Ordinal))
            text.Append(name).Append('\t').Append(answer.Fingerprint).Append('\t').Append(Describe(answer)).Append('\n');
        return text.ToString();
    }

    /// <summary>The approved answer as it is written in the file, also used in reports.</summary>
    public static string Describe(ApprovedAnswer answer)
    {
        var text = new StringBuilder(answer.AlreadyCropped ? AlreadyCropped : Describe(answer.Subject));
        if (answer.Tolerance != 0)
            text.Append(CultureInfo.InvariantCulture, $" {ToleranceFlag} {answer.Tolerance}");
        if (answer.Dark)
            text.Append(' ').Append(DarkFlag);
        return text.ToString();
    }

    /// <summary>Detection's answer as it is written in the file, also used in reports.</summary>
    public static string Describe(Finding found) => found.IsAlreadyCropped ? AlreadyCropped : Describe(found.Subject);

    private static string Describe(Subject? subject)
    {
        if (subject is null)
            return None;
        var text = new StringBuilder("window ").Append(Describe(subject.Window));
        foreach (var popup in subject.Popups)
            text.Append(" popup ").Append(Describe(popup));
        return text.ToString();
    }

    private static string Describe(PixelRect rect) =>
        string.Create(CultureInfo.InvariantCulture, $"{rect.X},{rect.Y},{rect.Width},{rect.Height}");

    /// <summary>Reads the answer field: the answer itself, then an optional <c>tolerance N</c>, then an optional <c>dark</c>.</summary>
    private static bool TryParseAnswer(string fingerprint, string text, out ApprovedAnswer answer)
    {
        answer = new ApprovedAnswer(fingerprint, null);
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var dark = words.Count > 0 && words[^1] == DarkFlag;
        if (dark)
            words.RemoveAt(words.Count - 1);
        var tolerance = 0;
        if (words.Count >= 2 && words[^2] == ToleranceFlag)
        {
            if (!int.TryParse(words[^1], NumberStyles.None, CultureInfo.InvariantCulture, out tolerance))
                return false;
            words.RemoveRange(words.Count - 2, 2);
        }

        var rest = string.Join(' ', words);
        if (rest == AlreadyCropped)
            answer = ApprovedAnswer.AlreadyCroppedAnswer(fingerprint);
        else if (TryParseSubject(rest, out var subject))
            answer = new ApprovedAnswer(fingerprint, subject);
        else
            return false;
        answer = answer with { Tolerance = tolerance, Dark = dark };
        return true;
    }

    private static bool TryParseSubject(string text, out Subject? subject)
    {
        subject = null;
        if (text == None)
            return true;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Length % 2 != 0 || words[0] != "window" || !TryParseRect(words[1], out var window))
            return false;
        var popups = new List<PixelRect>();
        for (var i = 2; i < words.Length; i += 2)
        {
            if (words[i] != "popup" || !TryParseRect(words[i + 1], out var popup))
                return false;
            popups.Add(popup);
        }
        subject = new Subject(window) { Popups = popups };
        return true;
    }

    private static bool TryParseRect(string text, out PixelRect rect)
    {
        rect = default;
        var parts = text.Split(',');
        if (parts.Length != 4)
            return false;
        var numbers = new int[4];
        for (var i = 0; i < 4; i++)
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
                return false;
        rect = new PixelRect(numbers[0], numbers[1], numbers[2], numbers[3]);
        return true;
    }
}
