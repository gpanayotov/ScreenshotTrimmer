using ScreenshotTrimmer.Processing.Detection;
using ScreenshotTrimmer.Processing.Imaging;

namespace ScreenshotTrimmer.Processing.Tests.Samples;

public class SampleVerdictTests
{
    private static readonly Subject Dialog = new(new PixelRect(10, 20, 300, 200));

    [Fact]
    public void The_approved_answer_found_again_passes()
    {
        Assert.Equal(SampleVerdict.Pass, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "aaaa", Dialog with { }));
    }

    [Fact]
    public void Nothing_found_again_where_nothing_was_approved_passes()
    {
        Assert.Equal(SampleVerdict.Pass, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", null), "aaaa", null));
    }

    [Fact]
    public void A_different_window_fails()
    {
        var moved = new Subject(new PixelRect(11, 20, 300, 200));

        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "aaaa", moved));
    }

    [Fact]
    public void A_missing_or_extra_popup_fails()
    {
        var withPopup = Dialog with { Popups = [new PixelRect(300, 40, 100, 80)] };

        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "aaaa", withPopup));
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", withPopup), "aaaa", Dialog));
    }

    [Fact]
    public void Nothing_found_where_a_Subject_was_approved_fails_and_the_reverse_too()
    {
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "aaaa", null));
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", null), "aaaa", Dialog));
    }

    [Fact]
    public void An_image_with_no_approved_answer_is_new()
    {
        Assert.Equal(SampleVerdict.New, SampleVerdicts.Judge(null, "aaaa", Dialog));
    }

    [Fact]
    public void An_image_whose_file_changed_since_its_answer_was_approved_is_changed_whatever_is_found()
    {
        Assert.Equal(SampleVerdict.Changed, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "bbbb", Dialog));
        Assert.Equal(SampleVerdict.Changed, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "bbbb", null));
    }
}
