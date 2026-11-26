using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class FirstStepTests
{
    private static readonly Crop DrawnCrop = Crop.Classic(new PixelRect(10, 20, 30, 40));

    private static Steps AddCrop(Steps steps) => steps with { Crop = DrawnCrop };

    [Fact]
    public void The_first_step_takes_its_finishing_from_the_remembered_settings()
    {
        var remembered = new UserSettings
        {
            FrameLines = true, RoundedCorners = true, ShadowOn = true, ShadowSize = 2.5, ShadowOpacity = 0.6,
        };

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: false, AddCrop);

        var expected = new Steps
        {
            Crop = DrawnCrop,
            FrameLines = true,
            RoundedCorners = new RoundedCorners(),
            Shadow = new Shadow { Size = 2.5, Opacity = 0.6 },
        };
        Assert.Equal(expected, steps);
    }

    [Fact]
    public void Finishing_switched_off_in_the_settings_is_left_out_of_the_first_step()
    {
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = false, ShadowOn = false };

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: false, AddCrop);

        Assert.Equal(new Steps { Crop = DrawnCrop }, steps);
    }

    [Fact]
    public void Shadow_is_left_off_for_an_original_that_already_has_shadow_padding()
    {
        var remembered = new UserSettings { FrameLines = true, RoundedCorners = false, ShadowOn = true };

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: true, AddCrop);

        Assert.Equal(new Steps { Crop = DrawnCrop, FrameLines = true }, steps);
    }

    [Fact]
    public void Later_steps_change_only_what_was_asked_and_ignore_the_settings()
    {
        var current = new Steps { Crop = Crop.Classic(new PixelRect(0, 0, 5, 5)) };
        var remembered = new UserSettings { FrameLines = true, RoundedCorners = true, ShadowOn = true };

        var steps = FirstStep.Apply(current, remembered, originalHasShadowPadding: false, AddCrop);

        Assert.Equal(new Steps { Crop = DrawnCrop }, steps);
    }
}
