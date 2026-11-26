namespace ScreenshotTrimmer.Processing.Files;

/// <summary>Turns the paths the user opened into the list of images to show.</summary>
public static class ImageListing
{
    private static readonly HashSet<string> ImageExtensions =
        new([".png", ".jpg", ".jpeg", ".webp", ".bmp"], StringComparer.OrdinalIgnoreCase);

    private static bool IsImagePath(string path) => ImageExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Lists the images for <paramref name="openedPaths"/>: one file gives every image in its folder with that file
    /// selected, a folder gives its images, and several paths give only those files (plus the images of any folder
    /// among them). Folders are not searched recursively, paths that are not PNG, JPEG, WebP or BMP files are skipped,
    /// and the list is sorted naturally.
    /// </summary>
    public static Listing List(IReadOnlyList<string> openedPaths, ListingOptions options)
    {
        ArgumentNullException.ThrowIfNull(openedPaths);
        ArgumentNullException.ThrowIfNull(options);

        var fullPaths = openedPaths.Select(Path.GetFullPath).ToList();
        string? openedFile = null;
        IEnumerable<string> candidates;
        if (fullPaths.Count == 1 && File.Exists(fullPaths[0]))
        {
            openedFile = fullPaths[0];
            candidates = Directory.EnumerateFiles(Path.GetDirectoryName(openedFile)!);
        }
        else
        {
            candidates = fullPaths.SelectMany(path =>
                Directory.Exists(path) ? Directory.EnumerateFiles(path) : File.Exists(path) ? [path] : []);
        }

        var paths = candidates.Where(IsImagePath).Distinct().ToList();
        var suffix = string.IsNullOrEmpty(options.ResultSuffix) ? null : options.ResultSuffix;
        var listedStems = paths.Select(StemOf).ToHashSet(FileNames.Comparer);
        var stemsOnDisk = suffix is null ? [] : ImageStemsInFoldersOf(paths);

        // A Result's Original must be listed too; otherwise the suffixed file is a normal image.
        bool IsResult(string path) =>
            suffix is not null
            && StemOf(path) is var stem
            && stem.EndsWith(suffix, FileNames.Comparison)
            && listedStems.Contains(stem[..^suffix.Length]);

        // The badge reflects the disk, so an Original opened without its Result is still marked.
        bool HasResult(string path) => suffix is not null && stemsOnDisk.Contains(StemOf(path) + suffix);

        var images = paths
            .Select(path => new ListedImage { Path = path, IsResult = IsResult(path), HasResult = HasResult(path) })
            // The file the user opened on its own is always shown, even when it is a hidden Result, as are kept Results.
            .Where(image => options.IncludeResults || !image.IsResult || image.Path == openedFile
                            || options.KeptResults.Contains(image.Path))
            // Base name before extension, so `shot.png` sorts before `shot-trimmed.png` and `shot-2.png`.
            .OrderBy(image => Path.GetFileNameWithoutExtension(image.Path), NaturalComparer.Instance)
            .ThenBy(image => Path.GetExtension(image.Path), NaturalComparer.Instance)
            .ThenBy(image => image.Path, NaturalComparer.Instance)
            .ToList();

        return new Listing
        {
            Images = images,
            Selected = images.FirstOrDefault(image => image.Path == openedFile),
        };
    }

    /// <summary>The stems of every image file in the folders that hold <paramref name="paths"/>.</summary>
    private static HashSet<string> ImageStemsInFoldersOf(IEnumerable<string> paths) =>
        paths.Select(path => Path.GetDirectoryName(path)!)
            .Distinct()
            .SelectMany(Directory.EnumerateFiles)
            .Where(IsImagePath)
            .Select(StemOf)
            .ToHashSet(FileNames.Comparer);

    /// <summary>The folder and base name of <paramref name="path"/>, without its extension.</summary>
    private static string StemOf(string path) =>
        Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path));
}
