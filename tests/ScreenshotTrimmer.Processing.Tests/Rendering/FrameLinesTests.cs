using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class FrameLinesTests
{
    private static readonly Steps FrameLinesOn = new() { FrameLines = true };

    [Fact]
    public void A_2px_strip_of_background_along_the_top_is_removed_exactly()
    {
        var original = Synthetic.WindowWithStrips(60, 40, top: 2);

        var output = Renderer.Render(original, FrameLinesOn);

        Assert.Equal(60, output.Result.Width);
        Assert.Equal(40, output.Result.Height);
        Assert.Equal(Synthetic.CheckerAt(0, 0), output.Result[0, 0]);
        Assert.Equal(2, output.Report.FrameLinesRemoved.Top);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(7)]
    public void A_strip_of_background_on_every_side_is_removed_exactly(int strip)
    {
        var original = Synthetic.WindowWithStrips(60, 40, left: strip, top: strip, right: strip, bottom: strip);

        var output = Renderer.Render(original, FrameLinesOn);

        Assert.Equal(60, output.Result.Width);
        Assert.Equal(40, output.Result.Height);
        Assert.Equal(Synthetic.CheckerAt(0, 0), output.Result[0, 0]);
        Assert.Equal(Synthetic.CheckerAt(59, 39), output.Result[59, 39]);
        Assert.Equal(new FrameLinesRemoved(strip, strip, strip, strip), output.Report.FrameLinesRemoved);
    }

    [Fact]
    public void The_report_gives_the_lines_removed_on_each_side()
    {
        var original = Synthetic.WindowWithStrips(60, 40, left: 3, top: 7, right: 0, bottom: 2);

        var output = Renderer.Render(original, FrameLinesOn);

        Assert.Equal(new FrameLinesRemoved(Left: 3, Top: 7, Right: 0, Bottom: 2), output.Report.FrameLinesRemoved);
        Assert.Equal(60, output.Result.Width);
        Assert.Equal(40, output.Result.Height);
    }

    [Fact]
    public void No_more_than_8_lines_are_removed_from_a_side()
    {
        // 10 lines above the window: 2 of a darker shadow, then 8 of background,
        // so every one of them differs from the line 8px further in.
        var original = Synthetic.WindowWithStrips(60, 40, top: 10);
        var shadow = new Rgba(190, 190, 190, 255);
        for (var y = 0; y < 2; y++)
            for (var x = 0; x < original.Width; x++)
                original[x, y] = shadow;

        var output = Renderer.Render(original, FrameLinesOn);

        Assert.Equal(8, output.Report.FrameLinesRemoved.Top);
        Assert.Equal(42, output.Result.Height);
        Assert.Equal(Synthetic.Background, output.Result[0, 0]);
    }

    [Fact]
    public void A_strip_of_real_content_along_an_edge_is_kept()
    {
        // A one-colour toolbar 12px tall: its outer row is uniform, but so is the row 8px in.
        var toolbar = new Rgba(90, 90, 90, 255);
        var original = Synthetic.WindowWithStrips(60, 40);
        for (var y = 0; y < 12; y++)
            for (var x = 0; x < original.Width; x++)
                original[x, y] = toolbar;

        var output = Renderer.Render(original, FrameLinesOn);

        Assert.True(output.Result.PixelsEqual(original));
        Assert.Equal(FrameLinesRemoved.None, output.Report.FrameLinesRemoved);
    }

    [Fact]
    public void With_the_frame_lines_switch_off_nothing_is_removed()
    {
        var original = Synthetic.WindowWithStrips(60, 40, left: 2, top: 2, right: 2, bottom: 2);

        var output = Renderer.Render(original, new Steps { FrameLines = false });

        Assert.True(output.Result.PixelsEqual(original));
        Assert.Equal(FrameLinesRemoved.None, output.Report.FrameLinesRemoved);
    }

    [Fact]
    public void Frame_lines_are_removed_from_the_crop_not_the_whole_image()
    {
        // The crop takes the window plus 3px of the background around it.
        var original = Synthetic.WindowWithStrips(60, 40, left: 20, top: 20, right: 20, bottom: 20);
        var steps = new Steps { Crop = Crop.Classic(new PixelRect(17, 17, 66, 46)), FrameLines = true };

        var output = Renderer.Render(original, steps);

        Assert.Equal(60, output.Result.Width);
        Assert.Equal(40, output.Result.Height);
        Assert.Equal(new FrameLinesRemoved(3, 3, 3, 3), output.Report.FrameLinesRemoved);
    }
}
