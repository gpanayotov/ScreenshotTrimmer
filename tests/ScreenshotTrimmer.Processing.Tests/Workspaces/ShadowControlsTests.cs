using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class ShadowControlsTests
{
    [Fact]
    public void An_image_with_no_steps_shows_the_remembered_shadow()
    {
        var remembered = new UserSettings { ShadowOn = true, ShadowSize = 2.5, ShadowOpacity = 0.6 };

        var shown = ShadowControls.Shown(Steps.None, remembered, originalHasShadowPadding: false);

        Assert.Equal(new ShadowControls(On: true, Size: 2.5, Opacity: 0.6), shown);
    }

    [Fact]
    public void An_original_with_shadow_padding_and_no_steps_shows_the_shadow_off_with_the_remembered_sliders()
    {
        var remembered = new UserSettings { ShadowOn = true, ShadowSize = 2.5, ShadowOpacity = 0.6 };

        var shown = ShadowControls.Shown(Steps.None, remembered, originalHasShadowPadding: true);

        Assert.Equal(new ShadowControls(On: false, Size: 2.5, Opacity: 0.6), shown);
    }

    [Fact]
    public void An_image_with_steps_shows_its_own_shadow_whatever_the_settings()
    {
        var steps = new Steps { Shadow = new Shadow { Size = 3, Opacity = 0.2 } };
        var remembered = new UserSettings { ShadowOn = false, ShadowSize = 1, ShadowOpacity = 0.5 };

        var shown = ShadowControls.Shown(steps, remembered, originalHasShadowPadding: true);

        Assert.Equal(new ShadowControls(On: true, Size: 3, Opacity: 0.2), shown);
    }

    [Fact]
    public void An_image_whose_steps_leave_out_the_shadow_shows_it_off_with_the_remembered_sliders()
    {
        var steps = new Steps { FrameLines = true };
        var remembered = new UserSettings { ShadowOn = true, ShadowSize = 1.5, ShadowOpacity = 0.4 };

        var shown = ShadowControls.Shown(steps, remembered, originalHasShadowPadding: false);

        Assert.Equal(new ShadowControls(On: false, Size: 1.5, Opacity: 0.4), shown);
    }

    [Fact]
    public void An_already_processed_original_seeds_with_shadow_off_even_when_the_shadow_setting_is_on()
    {
        var remembered = new UserSettings { FrameLines = true, RoundedCorners = false, ShadowOn = true };
        var drawn = Crop.Classic(new PixelRect(2, 2, 10, 10));

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: true, s => s with { Crop = drawn });

        Assert.Equal(new Steps { Crop = drawn, FrameLines = true }, steps);
        Assert.False(ShadowControls.Shown(steps, remembered, originalHasShadowPadding: true).On);
    }

    [Fact]
    public void The_shadow_can_still_be_turned_on_for_an_already_processed_original()
    {
        var remembered = new UserSettings { FrameLines = false, RoundedCorners = false, ShadowOn = true, ShadowSize = 2, ShadowOpacity = 0.5 };
        var wanted = new ShadowControls(On: true, Size: 2, Opacity: 0.5);

        var steps = FirstStep.Apply(Steps.None, remembered, originalHasShadowPadding: true, wanted.Change);

        Assert.Equal(new Steps { Shadow = new Shadow { Size = 2, Opacity = 0.5 } }, steps);
    }

    [Fact]
    public void Changing_the_shadow_changes_only_the_shadow()
    {
        var cropped = Crop.Classic(new PixelRect(1, 1, 8, 8));
        var current = new Steps { Crop = cropped, FrameLines = true, Shadow = new Shadow() };

        var resized = new ShadowControls(On: true, Size: 3.5, Opacity: 0.8).Change(current);
        var off = new ShadowControls(On: false, Size: 3.5, Opacity: 0.8).Change(resized);

        Assert.Equal(current with { Shadow = new Shadow { Size = 3.5, Opacity = 0.8 } }, resized);
        Assert.Equal(current with { Shadow = null }, off);
    }

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(-1.0, 0.5)]
    [InlineData(4.5, 0.5)]
    [InlineData(1.0, -0.1)]
    [InlineData(1.0, 1.1)]
    [InlineData(double.NaN, 0.5)]
    public void Sizes_and_opacities_outside_the_allowed_ranges_are_refused(double size, double opacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShadowControls(On: true, Size: size, Opacity: opacity));
    }

    [Fact]
    public void The_shadow_controls_become_the_remembered_settings_and_survive_a_restart()
    {
        using var folder = new TempFolder();
        var before = new UserSettings { FrameLines = false, ShadowOn = true, ShadowSize = 1, ShadowOpacity = 0.35 };
        var chosen = new ShadowControls(On: false, Size: 3.25, Opacity: 0.7);

        new SettingsStore(folder.Path).Save(chosen.Remember(before));
        var loaded = new SettingsStore(folder.Path).Load();

        Assert.Equal(chosen, ShadowControls.Shown(Steps.None, loaded, originalHasShadowPadding: false));
        Assert.False(loaded.FrameLines);
    }
}
