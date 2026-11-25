using Avalonia.Media.Imaging;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

/// <summary>
/// One image in the strip. Its thumbnail is loaded the first time the strip shows the entry, so only entries
/// scrolled into view are decoded.
/// </summary>
public sealed class StripEntry(WorkspaceImage image, ThumbnailCache thumbnails) : Observable
{
    private Bitmap? _thumbnail;
    private bool _isUnreadable;
    private CancellationTokenSource? _loading;

    public WorkspaceImage Image { get; } = image;

    public string Name => Path.GetFileName(Image.Path);

    /// <summary>
    /// The decoded thumbnail, or <c>null</c> while it loads or when the file cannot be read. Reading it starts the
    /// load, so it must only be read on the interface thread.
    /// </summary>
    public Bitmap? Thumbnail
    {
        get
        {
            if (_thumbnail is null && !_isUnreadable && _loading is null)
            {
                // A cached thumbnail is shown at once, without a placeholder in between.
                _thumbnail = thumbnails.TryGet(Image.Path, Image.ContentVersion);
                if (_thumbnail is null)
                    Load();
            }
            return _thumbnail;
        }
        private set => Set(ref _thumbnail, value);
    }

    /// <summary>Whether the file cannot be read, so the strip shows an error marker instead of a thumbnail.</summary>
    public bool IsUnreadable
    {
        get => _isUnreadable;
        private set => Set(ref _isUnreadable, value);
    }

    /// <summary>
    /// Called when the strip stops showing this entry: a load that has not started decoding is dropped, and starts
    /// again if the entry is shown again.
    /// </summary>
    public void Hidden()
    {
        _loading?.Cancel();
        _loading = null;
    }

    private async void Load()
    {
        var loading = _loading = new CancellationTokenSource();
        try
        {
            Thumbnail = await thumbnails.GetAsync(Image.Path, Image.ContentVersion, loading.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            IsUnreadable = true;
        }
        finally
        {
            if (_loading == loading)
                _loading = null;
            loading.Dispose();
        }
    }
    /// <summary>Whether this image's Result exists beside it, shown as the "has result" badge.</summary>
    public bool HasResult => Image.HasResult;

    /// <summary>Listed Results, shown only when the user asks for them, are dimmed so the Originals stand out.</summary>
    public double Opacity => Image.IsResult ? 0.6 : 1.0;
}
