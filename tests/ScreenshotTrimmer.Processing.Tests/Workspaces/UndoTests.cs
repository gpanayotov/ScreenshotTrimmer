using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Settings;
using ScreenshotTrimmer.Processing.Workspaces;
using SkiaSharp;

namespace ScreenshotTrimmer.Processing.Tests.Workspaces;

public class UndoTests
{
    private static Steps CropAt(int x) => new() { Crop = Crop.Classic(new PixelRect(x, 2, 3, 4)) };

    private static void Touch(TempFolder folder, params string[] names)
    {
        foreach (var name in names)
            File.WriteAllBytes(folder.File(name), [0]);
    }

    private static WorkspaceImage Entry(Workspace workspace, string name) =>
        workspace.Images.Single(i => Path.GetFileName(i.Path) == name);

    private static Workspace Opened(TempFolder folder, params string[] names)
    {
        Touch(folder, names);
        var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        return workspace;
    }

    [Fact]
    public void Undo_and_redo_step_back_and_forth_through_an_images_steps()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));
        workspace.SetSteps(image, CropAt(2));

        workspace.Undo(image);
        Assert.Equal(CropAt(1), workspace.Steps(image));
        workspace.Undo(image);
        Assert.Same(Steps.None, workspace.Steps(image));
        workspace.Redo(image);
        Assert.Equal(CropAt(1), workspace.Steps(image));
        workspace.Redo(image);
        Assert.Equal(CropAt(2), workspace.Steps(image));
    }

    [Fact]
    public void A_new_image_has_nothing_to_undo_or_redo()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");

        Assert.False(workspace.CanUndo(workspace.Images[0]));
        Assert.False(workspace.CanRedo(workspace.Images[0]));
    }

    [Fact]
    public void A_new_change_after_an_undo_clears_the_redo_history()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));
        workspace.SetSteps(image, CropAt(2));
        workspace.Undo(image);
        Assert.True(workspace.CanRedo(image));

        workspace.SetSteps(image, CropAt(3));
        workspace.Redo(image);

        Assert.False(workspace.CanRedo(image));
        Assert.Equal(CropAt(3), workspace.Steps(image));
        workspace.Undo(image);
        Assert.Equal(CropAt(1), workspace.Steps(image));
    }

    [Fact]
    public void Undoing_everything_leaves_nothing_to_undo()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));
        Assert.True(workspace.CanUndo(image));

        workspace.Undo(image);
        workspace.Undo(image);

        Assert.False(workspace.CanUndo(image));
        Assert.Same(Steps.None, workspace.Steps(image));
    }

    [Fact]
    public void Each_image_keeps_its_own_history_while_the_selection_moves()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png", "b.png");
        workspace.SetSteps(Entry(workspace, "a.png"), CropAt(1));
        workspace.SetSteps(Entry(workspace, "a.png"), CropAt(2));
        workspace.Next();
        workspace.SetSteps(Entry(workspace, "b.png"), CropAt(5));

        workspace.Undo(Entry(workspace, "b.png"));
        workspace.Previous();
        workspace.Undo(Entry(workspace, "a.png"));

        Assert.Equal(CropAt(1), workspace.Steps(Entry(workspace, "a.png")));
        Assert.Same(Steps.None, workspace.Steps(Entry(workspace, "b.png")));
        Assert.True(workspace.CanRedo(Entry(workspace, "b.png")));
        workspace.Redo(Entry(workspace, "b.png"));
        Assert.Equal(CropAt(5), workspace.Steps(Entry(workspace, "b.png")));
        Assert.Equal(CropAt(1), workspace.Steps(Entry(workspace, "a.png")));
    }

    [Fact]
    public void Undo_and_redo_announce_the_change()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));
        workspace.SetSteps(image, CropAt(2));
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Undo(image);
        workspace.Redo(image);

        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void Undo_and_redo_with_nothing_to_undo_or_redo_change_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Undo(workspace.Images[0]);
        workspace.Redo(workspace.Images[0]);

        Assert.Empty(changes);
        Assert.Same(Steps.None, workspace.Steps(workspace.Images[0]));
    }

    [Fact]
    public void Undoing_back_to_the_steps_at_the_last_save_clears_the_unsaved_marker_and_redoing_sets_it_again()
    {
        using var folder = new TempFolder();
        EncodedImages.Write(folder.File("shot.png"), EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        var saved = new Steps { Crop = Crop.Classic(new PixelRect(4, 2, 20, 10)) };
        workspace.SetSteps(Entry(workspace, "shot.png"), saved);
        workspace.Save(Entry(workspace, "shot.png"));
        workspace.SetSteps(Entry(workspace, "shot.png"), saved with { FrameLines = true });
        Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);

        workspace.Undo(Entry(workspace, "shot.png"));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
        workspace.Undo(Entry(workspace, "shot.png"));
        Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);
        workspace.Redo(Entry(workspace, "shot.png"));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
        workspace.Redo(Entry(workspace, "shot.png"));
        Assert.True(Entry(workspace, "shot.png").HasUnsavedSteps);
    }

    [Fact]
    public void Consecutive_steps_with_the_same_merge_key_are_undone_in_one_step()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));

        workspace.SetSteps(image, CropAt(2), mergeKey: "left");
        workspace.SetSteps(image, CropAt(3), mergeKey: "left");
        workspace.SetSteps(image, CropAt(4), mergeKey: "left");
        workspace.Undo(image);

        Assert.Equal(CropAt(1), workspace.Steps(image));
        workspace.Redo(image);
        Assert.Equal(CropAt(4), workspace.Steps(image));
    }

    [Fact]
    public void A_different_merge_key_or_a_step_without_one_starts_a_new_step()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];

        workspace.SetSteps(image, CropAt(1), mergeKey: "left");
        workspace.SetSteps(image, CropAt(2), mergeKey: "top");
        workspace.SetSteps(image, CropAt(3));
        workspace.SetSteps(image, CropAt(4), mergeKey: "top");

        workspace.Undo(image);
        Assert.Equal(CropAt(3), workspace.Steps(image));
        workspace.Undo(image);
        Assert.Equal(CropAt(2), workspace.Steps(image));
        workspace.Undo(image);
        Assert.Equal(CropAt(1), workspace.Steps(image));
    }

    [Fact]
    public void An_undo_ends_a_run_of_merged_steps()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1), mergeKey: "left");
        workspace.SetSteps(image, CropAt(2), mergeKey: "left");
        workspace.Undo(image);

        workspace.SetSteps(image, CropAt(3), mergeKey: "left");
        workspace.Undo(image);

        Assert.Same(Steps.None, workspace.Steps(image));
        Assert.False(workspace.CanUndo(image));
    }

    [Fact]
    public void Merged_steps_on_one_image_do_not_merge_with_another_images_steps()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png", "b.png");
        workspace.SetSteps(Entry(workspace, "a.png"), CropAt(1), mergeKey: "left");
        workspace.SetSteps(Entry(workspace, "b.png"), CropAt(1), mergeKey: "left");

        workspace.SetSteps(Entry(workspace, "a.png"), CropAt(2), mergeKey: "left");
        workspace.Undo(Entry(workspace, "a.png"));

        Assert.Same(Steps.None, workspace.Steps(Entry(workspace, "a.png")));
    }

    [Fact]
    public void A_save_ends_a_run_of_merged_steps_so_undo_can_return_to_the_saved_steps()
    {
        using var folder = new TempFolder();
        EncodedImages.Write(folder.File("shot.png"), EncodedImages.HalfAndHalf(40, 30, SKColors.Red, SKColors.Blue));
        using var workspace = new Workspace(new UserSettings());
        workspace.Open([folder.Path]);
        workspace.SetSteps(Entry(workspace, "shot.png"), CropAt(1), mergeKey: "left");
        workspace.Save(Entry(workspace, "shot.png"));

        workspace.SetSteps(Entry(workspace, "shot.png"), CropAt(2), mergeKey: "left");
        workspace.Undo(Entry(workspace, "shot.png"));

        Assert.Equal(CropAt(1), workspace.Steps(Entry(workspace, "shot.png")));
        Assert.False(Entry(workspace, "shot.png").HasUnsavedSteps);
    }

    [Fact]
    public void A_run_of_merged_steps_that_comes_back_to_its_start_leaves_no_step_to_undo()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));

        workspace.SetSteps(image, CropAt(2), mergeKey: "left");
        workspace.SetSteps(image, CropAt(1), mergeKey: "left");
        workspace.Undo(image);

        Assert.Same(Steps.None, workspace.Steps(image));
    }

    [Fact]
    public void Reset_removes_all_steps_as_one_undoable_step()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var image = workspace.Images[0];
        workspace.SetSteps(image, CropAt(1));
        workspace.SetSteps(image, CropAt(2) with { FrameLines = true });

        workspace.Reset(image);
        Assert.Same(Steps.None, workspace.Steps(image));
        Assert.False(workspace.Images[0].HasUnsavedSteps);

        workspace.Undo(image);
        Assert.Equal(CropAt(2) with { FrameLines = true }, workspace.Steps(image));
        Assert.True(workspace.Images[0].HasUnsavedSteps);
    }

    [Fact]
    public void Reset_of_an_image_without_steps_changes_nothing()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var changes = new List<WorkspaceChange>();
        workspace.Changed += (_, change) => changes.Add(change);

        workspace.Reset(workspace.Images[0]);

        Assert.Empty(changes);
        Assert.False(workspace.CanUndo(workspace.Images[0]));
    }

    [Fact]
    public void History_of_an_image_that_is_not_listed_is_refused()
    {
        using var folder = new TempFolder();
        using var workspace = Opened(folder, "a.png");
        var stranger = new WorkspaceImage { Path = folder.File("elsewhere.png") };

        Assert.Throws<ArgumentException>(() => workspace.Undo(stranger));
        Assert.Throws<ArgumentException>(() => workspace.Redo(stranger));
        Assert.Throws<ArgumentException>(() => workspace.CanUndo(stranger));
        Assert.Throws<ArgumentException>(() => workspace.CanRedo(stranger));
        Assert.Throws<ArgumentException>(() => workspace.Reset(stranger));
    }
}
