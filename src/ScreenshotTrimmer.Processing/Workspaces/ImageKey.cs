using ScreenshotTrimmer.Processing.Files;

namespace ScreenshotTrimmer.Processing.Workspaces;

/// <summary>
/// What identifies an entry in a <see cref="Workspace"/>'s list: the full path of its file, or, for a Pasted image that
/// has not been saved yet, its number. Paths compare as <see cref="FileNames.Same"/> does, so a key can be compared,
/// hashed and kept in a dictionary as it is.
/// </summary>
public sealed record ImageKey
{
    private ImageKey(string? path, int? pastedNumber)
    {
        Path = path;
        PastedNumber = pastedNumber;
    }

    /// <summary>The full path of the image file; <c>null</c> for a Pasted image that has not been saved yet.</summary>
    public string? Path { get; }

    /// <summary>
    /// For a Pasted image that has not been saved yet, its number in the order the images were pasted, starting at 1;
    /// otherwise <c>null</c>.
    /// </summary>
    public int? PastedNumber { get; }

    /// <summary>The key of the image file at <paramref name="path"/>.</summary>
    public static ImageKey OfFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return new ImageKey(path, null);
    }

    /// <summary>The key of the Pasted image numbered <paramref name="number"/>, not saved yet.</summary>
    public static ImageKey OfPasted(int number) => new(null, number);

    /// <summary>This key after the renames <paramref name="renamed"/> gives for paths; a Pasted image's is unchanged.</summary>
    public ImageKey Renamed(Func<string, string> renamed) => Path is { } path ? OfFile(renamed(path)) : this;

    public bool Equals(ImageKey? other) =>
        other is not null
        && (Path is null || other.Path is null ? Path == other.Path : FileNames.Same(Path, other.Path))
        && PastedNumber == other.PastedNumber;

    public override int GetHashCode() =>
        Path is { } path ? FileNames.Comparer.GetHashCode(path) : PastedNumber.GetHashCode();

    public override string ToString() => Path ?? $"Pasted image {PastedNumber}";
}
