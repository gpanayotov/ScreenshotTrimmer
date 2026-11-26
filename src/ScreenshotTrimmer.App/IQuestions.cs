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

/// <summary>Asks the user before unsaved work would be lost.</summary>
public interface IQuestions
{
    /// <summary>Asks whether to save all, discard or cancel before closing the window or replacing the list.</summary>
    Task<LeaveAnswer> AskToLeaveAsync();

    /// <summary>Asks whether to discard the unsaved Steps of the image named <paramref name="name"/> and remove it.</summary>
    /// <returns><c>true</c> to discard and remove; <c>false</c> to keep it.</returns>
    Task<bool> AskToDiscardAsync(string name);
}
