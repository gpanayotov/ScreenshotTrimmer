using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

public class ApprovedAnswersTests
{
    [Fact]
    public void A_written_file_reads_back_the_same_answers()
    {
        var answers = new Dictionary<string, ApprovedAnswer>
        {
            ["dialog.png"] = new("0123456789abcdef", new Subject(new PixelRect(10, 20, 300, 200))),
            ["Screenshot From 2026-11-25 20-59-27.png"] = new("fedcba9876543210", new Subject(new PixelRect(5, 6, 7, 8))
            {
                Popups = [new PixelRect(1, 2, 3, 4), new PixelRect(9, 10, 11, 12)],
            }),
            ["already-cropped.png"] = new("00000000ffffffff", null),
        };

        var read = ApprovedAnswers.Parse(ApprovedAnswers.Format(answers));

        Assert.Equal(answers.OrderBy(a => a.Key, StringComparer.Ordinal), read.OrderBy(a => a.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void A_hand_written_file_is_read_with_comments_and_blank_lines_ignored()
    {
        var text = """
            # Approved answers

            menu.png	aaaa	window 100,50,400,300 popup 480,120,160,200
            empty.png	bbbb	none
            """;

        var read = ApprovedAnswers.Parse(text);

        Assert.Equal(2, read.Count);
        Assert.Equal(
            new ApprovedAnswer("aaaa", new Subject(new PixelRect(100, 50, 400, 300)) { Popups = [new PixelRect(480, 120, 160, 200)] }),
            read["menu.png"]);
        Assert.Equal(new ApprovedAnswer("bbbb", null), read["empty.png"]);
    }

    [Fact]
    public void A_malformed_line_is_reported_with_its_line_number()
    {
        var error = Assert.Throws<FormatException>(() => ApprovedAnswers.Parse("a.png\taaaa\tnone\nb.png\tbbbb\twindow 1,2,3\n"));

        Assert.Contains("line 2", error.Message);
    }

    [Fact]
    public void Already_cropped_reads_and_writes_as_its_own_answer()
    {
        var read = ApprovedAnswers.Parse("cropped.png\taaaa\talready-cropped\n");

        Assert.Equal(ApprovedAnswer.AlreadyCroppedAnswer("aaaa"), read["cropped.png"]);
        Assert.NotEqual(new ApprovedAnswer("aaaa", null), read["cropped.png"]);
        Assert.Equal(read, ApprovedAnswers.Parse(ApprovedAnswers.Format(read)));
    }

    [Fact]
    public void A_tolerance_and_a_dark_flag_follow_any_answer_and_read_back_the_same()
    {
        var text = """
            dialog.png	aaaa	window 100,50,400,300 popup 480,120,160,200 tolerance 1 dark
            cropped.png	bbbb	already-cropped dark
            empty.png	cccc	none tolerance 1
            """;

        var read = ApprovedAnswers.Parse(text);

        Assert.Equal(
            new ApprovedAnswer("aaaa", new Subject(new PixelRect(100, 50, 400, 300)) { Popups = [new PixelRect(480, 120, 160, 200)] })
            {
                Tolerance = 1,
                Dark = true,
            },
            read["dialog.png"]);
        Assert.Equal(ApprovedAnswer.AlreadyCroppedAnswer("bbbb") with { Dark = true }, read["cropped.png"]);
        Assert.Equal(new ApprovedAnswer("cccc", null) { Tolerance = 1 }, read["empty.png"]);
        Assert.Equal(read, ApprovedAnswers.Parse(ApprovedAnswers.Format(read)));
    }

    [Theory]
    [InlineData("window 1,2,3,4 tolerance")]
    [InlineData("window 1,2,3,4 tolerance -1")]
    [InlineData("window 1,2,3,4 dark tolerance 1")]
    [InlineData("none bright")]
    public void A_misplaced_or_unknown_flag_is_malformed(string answer)
    {
        Assert.Throws<FormatException>(() => ApprovedAnswers.Parse($"a.png\taaaa\t{answer}\n"));
    }
}
