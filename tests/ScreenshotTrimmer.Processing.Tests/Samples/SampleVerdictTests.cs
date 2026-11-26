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

    [Fact]
    public void Within_the_tolerance_every_edge_of_the_window_and_popups_may_be_off_by_1px()
    {
        var approved = new ApprovedAnswer("aaaa", Dialog with { Popups = [new PixelRect(300, 40, 100, 80)] }) { Tolerance = 1 };
        var off = new Subject(new PixelRect(11, 19, 298, 201)) { Popups = [new PixelRect(299, 41, 101, 80)] };

        Assert.Equal(SampleVerdict.Pass, SampleVerdicts.Judge(approved, "aaaa", off));
    }

    [Fact]
    public void Beyond_the_tolerance_an_edge_fails()
    {
        var approved = new ApprovedAnswer("aaaa", Dialog) { Tolerance = 1 };

        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(approved, "aaaa", new Subject(new PixelRect(12, 20, 298, 200))));
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(approved, "aaaa", Dialog with { Popups = [new PixelRect(300, 40, 100, 80)] }));
    }

    [Fact]
    public void Without_a_tolerance_an_edge_1px_off_fails()
    {
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(new ApprovedAnswer("aaaa", Dialog), "aaaa", new Subject(new PixelRect(10, 20, 301, 200))));
    }

    [Fact]
    public void Already_cropped_passes_on_nothing_found_until_detection_can_tell_them_apart()
    {
        var approved = ApprovedAnswer.AlreadyCroppedAnswer("aaaa");

        Assert.Equal(SampleVerdict.Pass, SampleVerdicts.Judge(approved, "aaaa", null));
        Assert.Equal(SampleVerdict.Fail, SampleVerdicts.Judge(approved, "aaaa", Dialog));
    }
}
