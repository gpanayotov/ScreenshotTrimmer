using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;

namespace ScreenshotTrimmer.Processing.Tests.Rendering;

public class ShadowTests
{
    private static readonly Rgba Red = new(200, 30, 30, 255);

    private static RgbaImage Solid(int width, int height, Rgba colour)
    {
        var image = new RgbaImage(width, height);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                image[x, y] = colour;
        return image;
    }

    [Fact]
    public void At_size_one_the_Result_gets_14px_of_transparent_padding_on_every_side()
    {
        var original = Solid(100, 60, Red);

        var output = Renderer.Render(original, new Steps { Shadow = new Shadow() });

        Assert.Equal(128, output.Result.Width);
        Assert.Equal(88, output.Result.Height);
        Assert.Equal(14, output.Report.ShadowPadding);
        Assert.Equal(Red, output.Result[14, 14]);
        Assert.Equal(Red, output.Result[113, 73]);
        Assert.Equal(0, output.Result[0, 0].A);
        Assert.Equal(0, output.Result[127, 87].A);
    }

    [Theory]
    [InlineData(0.5, 8)]
    [InlineData(2.0, 26)]
    public void The_size_scales_the_padding_as_twice_the_blur_plus_the_offset(double size, int padding)
    {
        var original = Solid(100, 60, Red);

        var output = Renderer.Render(original, new Steps { Shadow = new Shadow { Size = size } });

        Assert.Equal(padding, output.Report.ShadowPadding);
        Assert.Equal(100 + 2 * padding, output.Result.Width);
        Assert.Equal(60 + 2 * padding, output.Result.Height);
        Assert.Equal(Red, output.Result[padding, padding]);
    }

    [Fact]
    public void The_shadow_is_black_and_darker_below_the_Subject_than_above_it()
    {
        var original = Solid(100, 60, Red);

        var result = Renderer.Render(original, new Steps { Shadow = new Shadow() }).Result;

        // The Subject spans rows 14..73. Along a long straight edge a Gaussian shadow (sigma 6) moved 2px down
        // covers the pixel just outside it by Phi(1.5 / 6) = 0.599 below and Phi(-2.5 / 6) = 0.338 above,
        // times the opacity 0.35: alpha 53 below and 30 above.
        var below = result[64, 74];
        var above = result[64, 13];
        Assert.Equal((0, 0, 0), (below.R, below.G, below.B));
        Assert.Equal((0, 0, 0), (above.R, above.G, above.B));
        Assert.InRange(below.A, 50, 56);
        Assert.InRange(above.A, 27, 33);
    }

    [Fact]
    public void The_opacity_setting_sets_how_dark_the_shadow_is()
    {
        var original = Solid(100, 60, Red);

        var result = Renderer.Render(original, new Steps { Shadow = new Shadow { Opacity = 0.7 } }).Result;

        // As above with opacity 0.7: 0.599 * 0.7 * 255 = 107 below, 0.338 * 0.7 * 255 = 60 above.
        Assert.InRange(result[64, 74].A, 104, 110);
        Assert.InRange(result[64, 13].A, 57, 63);
    }

    [Fact]
    public void The_shadow_follows_the_rounded_corners_cleared_before_it()
    {
        var original = Synthetic.RoundedWindow(160, 120, 16, 16, 16, 16);
        var steps = new Steps { RoundedCorners = new RoundedCorners(), Shadow = new Shadow() };

        var result = Renderer.Render(original, steps).Result;

        // The Subject's top-left pixel was cleared by the rounding, so only the soft shadow shows there,
        // and it is fainter than beside a straight edge, because the outline curves away from it.
        var corner = result[14, 14];
        Assert.Equal((0, 0, 0), (corner.R, corner.G, corner.B));
        Assert.InRange(corner.A, 1, 29);
        Assert.Equal(Synthetic.Window, result[14 + 80, 14]);
        Assert.Equal(Synthetic.Window, result[14 + 8, 14 + 8]);
    }

    [Fact]
    public void Rendering_the_same_Steps_twice_gives_identical_pixels()
    {
        var original = Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12);
        var steps = new Steps
        {
            Crop = new Crop(new PixelRect(0, 0, 160, 120)),
            FrameLines = true,
            RoundedCorners = new RoundedCorners(),
            Shadow = new Shadow { Size = 1.5, Opacity = 0.5 },
        };

        var first = Renderer.Render(original, steps);
        var second = Renderer.Render(original, steps);

        Assert.True(first.Result.PixelsEqual(second.Result));
        Assert.Equal(first.Report, second.Report);
        Assert.True(original.PixelsEqual(Synthetic.RoundedWindow(160, 120, 12, 12, 12, 12)));
    }
}
