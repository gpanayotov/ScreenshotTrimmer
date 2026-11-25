namespace ScreenshotTrimmer.Processing.Files;

/// <summary>A file a save moved, by full path.</summary>
public sealed record FileMove(string From, string To);
