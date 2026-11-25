using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class RoundedCornersTests
{
    private static readonly Steps RoundedOn = new() { RoundedCorners = new RoundedCorners() };

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(20)]
    public void A_known_radius_is_measured_within_one_pixel(int radius)
    {
        var original = Synthetic.RoundedWindow(160, 120, radius, radius, radius, radius);

        var measured = Renderer.Render(original, RoundedOn).Report.MeasuredRadii!;

        Assert.InRange(measured.TopLeft, radius - 1, radius + 1);
        Assert.InRange(measured.TopRight, radius - 1, radius + 1);
        Assert.InRange(measured.BottomRight, radius - 1, radius + 1);
        Assert.InRange(measured.BottomLeft, radius - 1, radius + 1);
    }

    [Fact]
    public void Pixels_outside_the_rounded_corners_become_transparent()
    {
        var original = Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12);

        var result = Renderer.Render(original, RoundedOn).Result;

        Assert.Equal(160, result.Width);
        Assert.Equal(120, result.Height);
        foreach (var (x, y) in new[] { (0, 0), (159, 0), (159, 119), (0, 119), (1, 1), (157, 2) })
            Assert.Equal(0, result[x, y].A);
        Assert.Equal(Synthetic.Window, result[80, 0]);
        Assert.Equal(Synthetic.Window, result[0, 60]);
        Assert.Equal(Synthetic.Window, result[6, 6]);
        Assert.Equal(Synthetic.Window, result[80, 60]);
    }

    [Fact]
    public void Clipping_is_antialiased_so_pixels_on_the_arc_are_partly_transparent()
    {
        var original = Synthetic.RoundedWindow(160, 120, 16, 16, 16, 16);

        var result = Renderer.Render(original, RoundedOn).Result;

        var partial = 0;
        for (var y = 0; y < 16; y++)
            for (var x = 0; x < 16; x++)
                if (result[x, y].A is > 0 and < 255)
                    partial++;
        Assert.True(partial >= 10, $"Expected a soft arc, found {partial} partly transparent pixels.");
        Assert.InRange(result[0, 11].A, 1, 254); // a radius-16 arc crosses the outer column between y 10.4 and 16
    }

    [Fact]
    public void Rounded_top_corners_and_square_bottom_corners_come_out_that_way()
    {
        var original = Synthetic.RoundedWindow(160, 120, 10, 10, 0, 0);

        var output = Renderer.Render(original, RoundedOn);

        Assert.Equal(0, output.Result[0, 0].A);
        Assert.Equal(0, output.Result[159, 0].A);
        Assert.Equal(Synthetic.Window, output.Result[0, 119]);
        Assert.Equal(Synthetic.Window, output.Result[159, 119]);
        var applied = output.Report.AppliedRadii!;
        Assert.InRange(applied.TopLeft, 9, 11);
        Assert.InRange(applied.TopRight, 9, 11);
        Assert.Equal(0, applied.BottomRight);
        Assert.Equal(0, applied.BottomLeft);
        Assert.Equal(0, output.Report.MeasuredRadii!.BottomLeft);
    }

    [Fact]
    public void Corners_that_agree_within_three_pixels_are_rounded_together_at_the_largest_radius()
    {
        var original = Synthetic.RoundedWindow(160, 120, 9, 11, 0, 0);

        var report = Renderer.Render(original, RoundedOn).Report;

        Assert.Equal(report.MeasuredRadii!.TopRight, report.AppliedRadii!.TopLeft);
        Assert.Equal(report.MeasuredRadii.TopRight, report.AppliedRadii.TopRight);
        Assert.True(report.MeasuredRadii.TopLeft < report.MeasuredRadii.TopRight);
    }

    [Fact]
    public void A_rounded_corner_that_agrees_with_no_other_stays_square()
    {
        var original = Synthetic.RoundedWindow(160, 120, 12, 0, 0, 0);

        var output = Renderer.Render(original, RoundedOn);

        Assert.InRange(output.Report.MeasuredRadii!.TopLeft, 11, 13);
        Assert.Equal(CornerRadii.Square, output.Report.AppliedRadii);
        Assert.True(output.Result.PixelsEqual(original));
    }

    [Fact]
    public void Corners_whose_radii_differ_by_more_than_three_pixels_are_not_rounded_together()
    {
        var original = Synthetic.RoundedWindow(160, 120, 6, 6, 20, 20);

        var applied = Renderer.Render(original, RoundedOn).Report.AppliedRadii!;

        Assert.InRange(applied.TopLeft, 5, 7);
        Assert.InRange(applied.TopRight, 5, 7);
        Assert.InRange(applied.BottomRight, 19, 21);
        Assert.InRange(applied.BottomLeft, 19, 21);
    }

    [Fact]
    public void Agreement_chains_so_every_corner_in_a_group_gets_the_same_radius()
    {
        // Measured about 9.5, 11.3 and 13.7: each agrees with the next, though the outer two differ by more than 3px.
        var original = Synthetic.RoundedWindow(160, 120, 9, 11, 14, 0);

        var report = Renderer.Render(original, RoundedOn).Report;

        var applied = report.AppliedRadii!;
        Assert.Equal(report.MeasuredRadii!.BottomRight, applied.TopLeft);
        Assert.Equal(applied.TopLeft, applied.TopRight);
        Assert.Equal(applied.TopLeft, applied.BottomRight);
        Assert.Equal(0, applied.BottomLeft);
    }

    [Fact]
    public void A_manual_radius_replaces_the_measurement_for_every_corner_that_would_be_rounded()
    {
        var original = Synthetic.RoundedWindow(160, 120, 10, 10, 0, 0);
        var steps = new Steps { RoundedCorners = new RoundedCorners { ManualRadius = 5 } };

        var output = Renderer.Render(original, steps);

        Assert.Equal(new CornerRadii(5, 5, 0, 0), output.Report.AppliedRadii);
        Assert.InRange(output.Report.MeasuredRadii!.TopLeft, 9, 11);
        Assert.Equal(0, output.Result[0, 0].A);
        Assert.True(output.Result[4, 0].A > 200, "Inside a radius-5 corner, so kept.");
        Assert.Equal(Synthetic.Window, output.Result[0, 119]);
    }

    [Fact]
    public void With_rounded_corners_switched_off_the_corners_are_left_as_they_are()
    {
        var original = Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12);

        var output = Renderer.Render(original, Steps.None);

        Assert.True(output.Result.PixelsEqual(original));
        Assert.Null(output.Report.MeasuredRadii);
        Assert.Null(output.Report.AppliedRadii);
    }

    [Fact]
    public void Rounded_corners_are_measured_and_rounded_on_the_cropped_window()
    {
        var screenshot = new RgbaImage(300, 200);
        var window = Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12);
        for (var y = 0; y < 200; y++)
            for (var x = 0; x < 300; x++)
                screenshot[x, y] = x is >= 50 and < 210 && y is >= 40 and < 160 ? window[x - 50, y - 40] : Synthetic.Page;
        var steps = RoundedOn with { Crop = Crop.Classic(new PixelRect(50, 40, 160, 120)) };

        var first = Renderer.Render(screenshot, steps);
        var second = Renderer.Render(screenshot, steps);

        Assert.InRange(first.Report.AppliedRadii!.BottomLeft, 11, 13);
        Assert.Equal(0, first.Result[0, 0].A);
        Assert.Equal(0, first.Result[159, 119].A);
        Assert.True(first.Result.PixelsEqual(second.Result));
        Assert.Equal(Synthetic.Page, screenshot[50, 40]);
    }
}
