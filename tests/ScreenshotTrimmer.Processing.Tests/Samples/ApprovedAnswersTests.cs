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
}
