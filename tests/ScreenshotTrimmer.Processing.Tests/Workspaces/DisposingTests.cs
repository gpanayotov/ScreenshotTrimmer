using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

/// <summary>A disposed workspace refuses every update, as <see cref="Workspace.Open"/> does, and announces nothing.</summary>
public class DisposingTests
{
    private static (Workspace Workspace, WorkspaceImage Image) DisposedWithOneListed(TempFolder folder, List<WorkspaceChange> changes)
    {
        File.WriteAllBytes(folder.File("a.png"), [0]);
        File.WriteAllBytes(folder.File("b.png"), [0]);
        var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var image = workspace.Images[1];
        workspace.Dispose();
        workspace.Changed += (_, change) => changes.Add(change);
        return (workspace, image);
    }

    public static TheoryData<string> Updates => ["Open", "ApplySettings", "ShowResults", "Remove", "Select", "Next", "Previous"];

    [Theory]
    [MemberData(nameof(Updates))]
    public void Every_update_after_disposing_throws_and_announces_nothing(string update)
    {
        using var folder = new TempFolder();
        var changes = new List<WorkspaceChange>();
        var (workspace, image) = DisposedWithOneListed(folder, changes);

        Action act = update switch
        {
            "Open" => () => workspace.Open([folder.Path]),
            "ApplySettings" => () => workspace.ApplySettings(new UserSettings { SaveMode = SaveMode.Backup }),
            "ShowResults" => () => workspace.ShowResults = true,
            "Remove" => () => workspace.Remove(image),
            "Select" => () => workspace.Select(image),
            "Next" => workspace.Next,
            "Previous" => workspace.Previous,
            _ => throw new ArgumentOutOfRangeException(nameof(update)),
        };

        Assert.Throws<ObjectDisposedException>(act);
        Assert.Empty(changes);
    }

    [Fact]
    public void Disposing_twice_is_harmless()
    {
        var workspace = new Workspace(new UserSettings());
        workspace.Dispose();
        workspace.Dispose();
    }
}
