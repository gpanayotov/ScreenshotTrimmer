using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class AlreadyProcessedTests
{
    [Fact]
    public void A_Result_rendered_with_a_Shadow_is_reported_as_already_having_shadow_padding()
    {
        var shadowed = Renderer.Render(Synthetic.RoundedWindow(120, 80, 8, 8, 8, 8), new Steps { Shadow = new Shadow() }).Result;

        var report = Renderer.Render(shadowed, Steps.None).Report;

        Assert.True(report.OriginalHasShadowPadding);
    }

    [Fact]
    public void An_ordinary_screenshot_is_not_reported_as_already_having_shadow_padding()
    {
        var report = Renderer.Render(Synthetic.Gradient(120, 80), Steps.None).Report;

        Assert.False(report.OriginalHasShadowPadding);
    }

    /// <summary>
    /// A 26x26 opaque image whose 100 border pixels are given <paramref name="borderAlpha"/>,
    /// except the first <paramref name="opaqueOnBorder"/> of them along the top row.
    /// </summary>
    private static RgbaImage Bordered(byte borderAlpha, int opaqueOnBorder)
    {
        var image = new RgbaImage(26, 26);
        for (var y = 0; y < 26; y++)
            for (var x = 0; x < 26; x++)
            {
                var onBorder = x == 0 || y == 0 || x == 25 || y == 25;
                var alpha = onBorder && !(y == 0 && x < opaqueOnBorder) ? borderAlpha : (byte)255;
                image[x, y] = new Rgba(10, 20, 30, alpha);
            }
        return image;
    }

    [Theory]
    [InlineData(9, true)]   // 91 of 100 border samples transparent
    [InlineData(10, false)] // exactly 90 is not more than 90%
    public void More_than_90_percent_of_the_border_must_be_transparent(int opaqueOnBorder, bool expected)
    {
        var report = Renderer.Render(Bordered(0, opaqueOnBorder), Steps.None).Report;

        Assert.Equal(expected, report.OriginalHasShadowPadding);
    }

    [Theory]
    [InlineData(39, true)]
    [InlineData(40, false)]
    public void A_border_sample_counts_as_transparent_when_its_alpha_is_below_40(byte borderAlpha, bool expected)
    {
        var report = Renderer.Render(Bordered(borderAlpha, 0), Steps.None).Report;

        Assert.Equal(expected, report.OriginalHasShadowPadding);
    }
}
