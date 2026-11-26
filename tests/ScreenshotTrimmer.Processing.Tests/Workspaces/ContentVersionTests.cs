using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class ContentVersionTests
{
    private static WorkspaceImage Listed(Workspace workspace, string path) =>
        workspace.Images.Single(image => image.Path == path);

    [Fact]
    public void An_overwritten_file_is_listed_with_a_new_content_version()
    {
        using var folder = new TempFolder();
        var shot = folder.File("shot.png");
        File.WriteAllBytes(shot, [1]);
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var before = Listed(workspace, shot).ContentVersion;

        File.WriteAllBytes(shot, [2, 2]);
        File.SetLastWriteTimeUtc(shot, DateTime.UtcNow.AddSeconds(5));
        workspace.Open([folder.Path]);

        Assert.NotEqual(before, Listed(workspace, shot).ContentVersion);
    }

    [Fact]
    public void An_untouched_file_keeps_its_content_version_when_listed_again()
    {
        using var folder = new TempFolder();
        var shot = folder.File("shot.png");
        File.WriteAllBytes(shot, [1]);
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var before = Listed(workspace, shot).ContentVersion;

        workspace.Open([folder.Path]);

        Assert.Equal(before, Listed(workspace, shot).ContentVersion);
    }
}
