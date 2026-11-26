using ScreenshotTrimmer.Processing.SelfChecking;

namespace ScreenshotTrimmer.Processing.Tests.SelfChecking;

public class SelfCheckTests
{
    [Fact]
    public void The_self_check_passes_where_the_image_libraries_load()
    {
        var result = SelfCheck.Run();

        Assert.True(result.Passed, result.Failure);
    }
}
