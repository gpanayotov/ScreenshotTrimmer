namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Why a save could not write its target.</summary>
public enum SaveFailureReason
{
    /// <summary>A folder exists under the target's name.</summary>
    TargetIsFolder,

    /// <summary>A file exists at the target and the caller did not allow overwriting it.</summary>
    TargetExists,

    /// <summary>The target's folder does not exist.</summary>
    FolderNotFound,

    /// <summary>The user may not write to the target or its folder.</summary>
    AccessDenied,

    /// <summary>Something else went wrong; the inner exception has the detail.</summary>
    Other,
}

/// <summary>
/// A save that could not complete. Nothing is left behind: no partial file under the target name,
/// no temporary file, and an Original moved in Backup mode is moved back.
/// </summary>
public sealed class SaveFailedException(string targetPath, SaveFailureReason reason, Exception? innerException = null)
    : IOException(DescribeFailure(targetPath, reason, innerException), innerException)
{
    /// <summary>Full path of the file the save tried to write.</summary>
    public string TargetPath { get; } = targetPath;

    /// <summary>Why the save failed.</summary>
    public SaveFailureReason Reason { get; } = reason;

    private static string DescribeFailure(string targetPath, SaveFailureReason reason, Exception? inner) => reason switch
    {
        SaveFailureReason.TargetIsFolder => $"Cannot save '{targetPath}': a folder already has that name.",
        SaveFailureReason.TargetExists => $"Cannot save '{targetPath}': a file with that name already exists.",
        SaveFailureReason.FolderNotFound => $"Cannot save '{targetPath}': the folder does not exist.",
        SaveFailureReason.AccessDenied => $"Cannot save '{targetPath}': permission to write there was denied.",
        _ => $"Cannot save '{targetPath}': {inner?.Message ?? "unknown error"}",
    };
}
