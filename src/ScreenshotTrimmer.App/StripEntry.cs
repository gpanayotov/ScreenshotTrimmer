using Avalonia.Media.Imaging;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Imaging;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>
/// One image in the strip. Its thumbnail is loaded the first time the strip shows the entry, so only entries
/// scrolled into view are decoded, and let go when the strip stops showing it.
/// </summary>
/// <param name="loadPasted">Gives a Pasted image's pixels, which its thumbnail is made from since it has no file.</param>
public sealed class StripEntry(WorkspaceImage image, ThumbnailCache thumbnails, Func<WorkspaceImage, RgbaImage> loadPasted)
    : Observable
{
    private ThumbnailCache.Lease? _thumbnail;
    private bool _isUnreadable;
    private CancellationTokenSource? _loading;
    private bool _unloading;

    public WorkspaceImage Image { get; private set; } = image;

    public string Name => DisplayName(Image);

    /// <summary>What the user calls <paramref name="image"/>: its file's name, or "Pasted image 1" until it is saved.</summary>
    public static string DisplayName(WorkspaceImage image) =>
        image.Path is { } path
            ? Path.GetFileName(path)
            : Localizer.Instance.Format("PastedImageName", image.PastedNumber);

    /// <summary>Whether this image's Result exists beside it, shown as the "has result" badge.</summary>
    public bool HasResult => Image.HasResult;

    /// <summary>Whether this image has Steps that are not saved, shown as the unsaved marker.</summary>
    public bool HasUnsavedSteps => Image.HasUnsavedSteps;

    /// <summary>Listed Results, shown only when the user asks for them, are dimmed so the Originals stand out.</summary>
    public double Opacity => Image.IsResult ? 0.6 : 1.0;

    /// <summary>
    /// Whether <paramref name="image"/> is this entry's image with the same content, so the entry can show it by
    /// <see cref="Update"/> and keep its thumbnail.
    /// </summary>
    public bool Shows(WorkspaceImage image) =>
        image.Key == Image.Key && image.ContentVersion == Image.ContentVersion;

    /// <summary>Shows the markers and badges of <paramref name="image"/>, which this entry <see cref="Shows"/>.</summary>
    public void Update(WorkspaceImage image)
    {
        if (image == Image)
            return;
        Image = image;
        Raise(nameof(HasResult));
        Raise(nameof(HasUnsavedSteps));
        Raise(nameof(Opacity));
    }

    /// <summary>
    /// The decoded thumbnail, or <c>null</c> while it loads or when the file cannot be read. Reading it starts the
    /// load, so it must only be read on the interface thread.
    /// </summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_isUnreadable && _loading is null && !_unloading)
            {
                // A cached thumbnail is shown at once, without a placeholder in between.
                _thumbnail = thumbnails.TryRent(Image.Key, Image.ContentVersion);
                if (_thumbnail is null)
                    Load();
            }
            return _thumbnail?.Bitmap;
        }
    }

    /// <summary>Whether the file cannot be read, so the strip shows an error marker instead of a thumbnail.</summary>
    public bool IsUnreadable
    {
        get => _isUnreadable;
        private set => Set(ref _isUnreadable, value);
    }

    /// <summary>
    /// Called when the strip stops showing this entry: drops a load that has not started decoding and lets go of the
    /// thumbnail, so the cache may dispose it. Showing the entry again loads it again, from the cache if it is still
    /// there.
    /// </summary>
    public void Unload()
    {
        _loading?.Cancel();
        _loading = null;
        // The strip drops the bitmap at once, without the change starting another load.
        _unloading = true;
        SetThumbnail(null);
        _unloading = false;
    }

    private async void Load()
    {
        var loading = _loading = new CancellationTokenSource();
        try
        {
            var image = Image;
            var thumbnail = image.Path is { } path
                ? await thumbnails.RentAsync(path, image.ContentVersion, loading.Token)
                : await thumbnails.RentAsync(image.Key, image.ContentVersion,
                    () => ThumbnailDecoding.FromPixels(loadPasted(image), (int)ThumbnailCache.Width, (int)ThumbnailCache.Height),
                    loading.Token);
            if (_loading == loading)
                SetThumbnail(thumbnail);
            else
                thumbnail.Dispose();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            // Unreadable, not an image, or anything unforeseen while decoding: the strip shows the error marker.
            IsUnreadable = true;
        }
        finally
        {
            if (_loading == loading)
                _loading = null;
            loading.Dispose();
        }
    }

    private void SetThumbnail(ThumbnailCache.Lease? thumbnail)
    {
        var old = _thumbnail;
        _thumbnail = thumbnail;
        Raise(nameof(Thumbnail));
        old?.Dispose();
    }
}
