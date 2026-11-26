using System.Runtime.InteropServices;

namespace ScreenshotTrimmer.App;

/// <summary>
/// Lets the Windows executable print to the console it was started from. It is a GUI-subsystem program (WinExe), so
/// Windows gives it no console: started from a plain cmd window, whatever it prints goes nowhere.
/// </summary>
internal static partial class ParentConsole
{
    private const int StdOutputHandle = -11;
    private const int AttachParentProcess = -1;

    /// <summary>
    /// On Windows, when standard output goes nowhere, attaches to the parent process's console so later writes to
    /// <see cref="Console.Out"/> show there. Output already redirected to a file or pipe, as CI does, is left alone.
    /// Call it before the first use of <see cref="Console"/>.
    /// </summary>
    public static void AttachWhenOutputGoesNowhere()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var output = GetStdHandle(StdOutputHandle);
        if (output != 0 && output != -1)
            return;
        // Fails harmlessly when the parent has no console, as when started from Explorer.
        AttachConsole(AttachParentProcess);
    }

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);
}
