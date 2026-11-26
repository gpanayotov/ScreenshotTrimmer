using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>What the user chose when leaving, or replacing the list, would lose unsaved work.</summary>
public enum LeaveAnswer
{
    /// <summary>Stay, with everything as it was.</summary>
    Cancel,

    /// <summary>Save every image with unsaved Steps first, and leave only if all of them save.</summary>
    SaveAll,

    /// <summary>Leave, discarding the unsaved Steps.</summary>
    Discard,
}

/// <summary>Asks the user before unsaved work would be lost, or before a Batch starts.</summary>
public interface IQuestions
{
    /// <summary>Asks whether to save all, discard or cancel before closing the window or replacing the list.</summary>
    Task<LeaveAnswer> AskToLeaveAsync();

    /// <summary>Asks whether to discard the unsaved Steps of the image named <paramref name="name"/> and remove it.</summary>
    /// <returns><c>true</c> to discard and remove; <c>false</c> to keep it.</returns>
    Task<bool> AskToDiscardAsync(string name);

    /// <summary>Asks whether to replace the existing file at <paramref name="path"/>.</summary>
    /// <returns><c>true</c> to replace it; <c>false</c> to leave it alone.</returns>
    Task<bool> AskToOverwriteAsync(string path);

    /// <summary>Asks whether to start a Batch that would do <paramref name="plan"/>, showing what it processes and skips.</summary>
    /// <returns><c>true</c> to start it; <c>false</c> to leave everything as it is.</returns>
    Task<bool> AskToStartBatchAsync(BatchPlan plan);
}
