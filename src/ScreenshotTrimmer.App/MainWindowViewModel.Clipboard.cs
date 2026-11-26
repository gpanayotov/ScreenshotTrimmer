using System.Globalization;
using ScreenshotTrimmer.App.Imaging;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Rendering;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// The clipboard: Pasted images in, Results out, and choosing where a Pasted image is saved.
public sealed partial class MainWindowViewModel
{
    private readonly IPathPicker _picker;
    private readonly IImageClipboard _clipboard;

    /// <summary>
    /// Adds the clipboard's image to the end of the strip as a Pasted image, selected; a notice says so when the
    /// clipboard holds no image.
    /// </summary>
    public RelayCommand PasteImageCommand { get; }

    /// <summary>Puts the selected image's current Result on the clipboard as a PNG with its transparency.</summary>
    public RelayCommand CopyResultCommand { get; }

    /// <summary>The name the save picker suggests for a Pasted image saved at <paramref name="now"/>.</summary>
    private static string SuggestedPastedName(DateTime now) =>
        "screenshot-" + now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture) + ".png";

    private async void PasteImage()
    {
        try
        {
            if (await _clipboard.TryGetImageAsync() is { } pixels)
            {
                _workspace.Paste(pixels);
                return;
            }
            _notices.Add(new Message("ClipboardHasNoImage"));
            RaiseNotices();
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
    }

    private async void CopyResult()
    {
        // The shown image's Original is the one loaded; while another loads there is nothing to copy yet.
        if (_workspace.Selected is not { } image || _original is not { } original || !SamePath(image.Key, _shown?.Key))
            return;
        try
        {
            var steps = _workspace.Steps(image);
            var result = await Task.Run(() => Renderer.Render(original, steps).Result);
            await _clipboard.SetImageAsync(result);
            _notices.Add(new Message("ResultCopied", StripEntry.DisplayName(image)));
            RaiseNotices();
        }
        catch (ArgumentException)
        {
            // The image left the list meanwhile.
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
    }

    /// <summary>
    /// Asks where to save the Pasted image <paramref name="image"/>, suggesting the last folder saved to or opened and a
    /// dated name, asks before replacing an existing file, and saves its Result there off the interface thread.
    /// </summary>
    /// <returns>The file written, or <c>null</c> when the user cancelled.</returns>
    /// <exception cref="Processing.Files.SaveFailedException">The Result could not be written.</exception>
    /// <exception cref="ArgumentException">The image is no longer listed as an unsaved Pasted image.</exception>
    private async Task<string?> SavePastedAsync(WorkspaceImage image)
    {
        var path = await _picker.PickSaveFileAsync(_lastFolder, SuggestedPastedName(DateTime.Now),
            StripEntry.DisplayName(image));
        if (path is null)
            return null;
        var overwrite = File.Exists(path);
        if (overwrite && !await _questions.AskToOverwriteAsync(path))
            return null;
        var report = await Task.Run(() => _workspace.SavePasted(image, path, overwrite));
        var written = report.Written[0];
        _lastFolder = FolderOf(written) ?? _lastFolder;
        return written;
    }
}
