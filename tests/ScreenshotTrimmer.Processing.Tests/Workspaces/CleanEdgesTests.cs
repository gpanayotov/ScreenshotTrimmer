using System.Globalization;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class CleanEdgesTests
{
    [Fact]
    public void An_image_with_no_steps_shows_the_remembered_switches_and_an_automatic_radius()
    {
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = true };

        var shown = CleanEdges.Shown(Steps.None, remembered);

        Assert.Equal(new CleanEdges(FrameLines: false, RoundedCorners: true, ManualRadius: null), shown);
    }

    [Fact]
    public void An_image_with_steps_shows_its_own_choices_whatever_the_settings()
    {
        var steps = new Steps { FrameLines = true, RoundedCorners = new RoundedCorners { ManualRadius = 6 } };
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = false };

        var shown = CleanEdges.Shown(steps, remembered);

        Assert.Equal(new CleanEdges(FrameLines: true, RoundedCorners: true, ManualRadius: 6), shown);
    }

    [Fact]
    public void An_image_whose_steps_leave_out_clean_edges_shows_both_switches_off()
    {
        var steps = new Steps { Crop = Crop.Classic(new PixelRect(0, 0, 4, 4)) };

        var shown = CleanEdges.Shown(steps, new UserSettings { FrameLines = true, RoundedCorners = true });

        Assert.Equal(new CleanEdges(FrameLines: false, RoundedCorners: false, ManualRadius: null), shown);
    }

    [Fact]
    public void A_first_finishing_change_seeds_the_rest_of_the_finishing_from_the_settings()
    {
        var remembered = new UserSettings
        {
            FrameLines = true, RoundedCorners = true, Shadow = true, ShadowSize = 2, ShadowOpacity = 0.5,
        };

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: false, CleanEdges.SetFrameLines(false));

        var expected = new Steps
        {
            FrameLines = false,
            RoundedCorners = new RoundedCorners(),
            Shadow = new Shadow { Size = 2, Opacity = 0.5 },
        };
        Assert.Equal(expected, steps);
    }

    [Fact]
    public void Switching_rounded_corners_changes_only_that_switch()
    {
        var cropped = Crop.Classic(new PixelRect(1, 1, 8, 8));
        var current = new Steps { Crop = cropped, FrameLines = true };

        var on = CleanEdges.SetRoundedCorners(true)(current);
        var off = CleanEdges.SetRoundedCorners(false)(on);

        Assert.Equal(new Steps { Crop = cropped, FrameLines = true, RoundedCorners = new RoundedCorners() }, on);
        Assert.Equal(current, off);
    }

    [Fact]
    public void Switching_rounded_corners_on_when_already_on_keeps_the_manual_radius()
    {
        var current = new Steps { RoundedCorners = new RoundedCorners { ManualRadius = 5 } };

        Assert.Equal(current, CleanEdges.SetRoundedCorners(true)(current));
    }

    [Fact]
    public void A_manual_radius_replaces_the_measurement_and_can_be_cleared_back_to_automatic()
    {
        var current = new Steps { FrameLines = true, RoundedCorners = new RoundedCorners() };

        var manual = CleanEdges.SetManualRadius(7)(current);
        var automatic = CleanEdges.SetManualRadius(null)(manual);

        Assert.Equal(new Steps { FrameLines = true, RoundedCorners = new RoundedCorners { ManualRadius = 7 } }, manual);
        Assert.Equal(current, automatic);
    }

    [Fact]
    public void A_manual_radius_on_an_image_with_no_steps_seeds_rounded_corners_from_the_settings()
    {
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = true, Shadow = false };

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: false, CleanEdges.SetManualRadius(9));

        Assert.Equal(new Steps { RoundedCorners = new RoundedCorners { ManualRadius = 9 } }, steps);
    }

    [Fact]
    public void A_manual_radius_does_nothing_while_rounded_corners_are_off()
    {
        var current = new Steps { FrameLines = true };

        Assert.Equal(current, CleanEdges.SetManualRadius(4)(current));
    }

    [Theory]
    [InlineData("", "en", null)]
    [InlineData("   ", "en", null)]
    [InlineData("8", "en", 8.0)]
    [InlineData(" 12 ", "en", 12.0)]
    [InlineData("6.5", "en", 6.5)]
    [InlineData("6,5", "bg", 6.5)]
    [InlineData("0", "en", 0.0)]
    public void A_manual_radius_is_read_in_the_interface_language_and_empty_means_automatic(
        string text, string culture, double? expected)
    {
        Assert.True(CleanEdges.TryParseManualRadius(text, CultureInfo.GetCultureInfo(culture), out var radius));
        Assert.Equal(expected, radius);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e9")]
    public void A_manual_radius_must_be_a_number_of_pixels_from_0_to_the_largest_allowed(string text)
    {
        Assert.False(CleanEdges.TryParseManualRadius(text, CultureInfo.InvariantCulture, out _));
    }
}
