using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// Saving the selected image's Result, or every unsaved one, and reporting how it went.
public sealed partial class MainWindowViewModel
{
    private readonly HashSet<ImageKey> _saving = [];
    private bool _savingAll;

    /// <summary>
    /// Saves the selected image's Result off the interface thread and reports the file written, or why it could not be,
    /// as a notice. Enabled while the selected image has unsaved Steps and is not already being saved.
    /// </summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>
    /// Saves the Result of every image with unsaved Steps off the interface thread, then reports how many were saved
    /// and why each failure failed as notices. Enabled while some image has unsaved Steps and no save is running.
    /// </summary>
    public RelayCommand SaveAllCommand { get; }

    private bool CanSave() =>
        _workspace.Selected is { HasUnsavedSteps: true } selected && !_saving.Contains(selected.Key) && !_savingAll;

    private bool CanSaveAll() => !_savingAll && _saving.Count == 0 && _workspace.Images.Any(image => image.HasUnsavedSteps);

    private void NotifySaveCommands()
    {
        SaveCommand.NotifyCanExecuteChanged();
        SaveAllCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Saves the selected image's Result off the interface thread and reports how it went as a notice. A Pasted image
    /// not saved yet asks where to save it first.
    /// </summary>
    private async void Save()
    {
        if (_workspace.Selected is not { } image || !_saving.Add(image.Key))
            return;
        NotifySaveCommands();
        try
        {
            if (image.IsPasted)
            {
                if (await SavePastedAsync(image) is { } written)
                    _notices.Add(new Message("Saved", written));
            }
            else
            {
                var report = await Task.Run(() => _workspace.Save(image));
                _notices.Add(new Message("Saved", string.Join(", ", report.Written)));
            }
        }
        catch (ArgumentException)
        {
            // The image left the list while the save was starting; the strip already shows why.
        }
        catch (Exception e)
        {
            // The Result could not be written, the Original could not be read, or anything unforeseen.
            _notices.Add(SaveFailedNotice(image, e));
        }
        finally
        {
            _saving.Remove(image.Key);
            NotifySaveCommands();
        }
        RaiseNotices();
    }

    /// <summary>Saves every image with unsaved Steps off the interface thread and reports how it went as notices.</summary>
    private async void SaveAll() => await SaveAllAsync();

    /// <summary>
    /// Saves every image with unsaved Steps off the interface thread and reports how it went as notices. Each Pasted
    /// image not saved yet asks where to save it in turn, and the user may skip one, which leaves it unsaved.
    /// </summary>
    /// <returns>
    /// Whether every one saved; <c>false</c> too when a save is already running or a Pasted image was skipped.
    /// </returns>
    private async Task<bool> SaveAllAsync()
    {
        if (_savingAll)
            return false;
        _savingAll = true;
        NotifySaveCommands();
        var savedAll = false;
        try
        {
            var outcomes = await Task.Run(_workspace.SaveAll);
            var saved = outcomes.Count(outcome => outcome.Report is not null);
            var failures = outcomes
                .Where(outcome => outcome.Failure is not null)
                .Select(outcome => SaveFailedNotice(outcome.Image, outcome.Failure!))
                .ToList();
            foreach (var pasted in outcomes.Where(outcome => outcome.NeedsLocation))
            {
                try
                {
                    if (await SavePastedAsync(pasted.Image) is not null)
                        saved++;
                }
                catch (ArgumentException)
                {
                    // Removed from the list while the user was choosing where to save an earlier one.
                }
                catch (Exception e)
                {
                    failures.Add(SaveFailedNotice(pasted.Image, e));
                }
            }
            _notices.Add(new Message("SavedAll", saved, outcomes.Count));
            _notices.AddRange(failures);
            savedAll = saved == outcomes.Count;
        }
        catch (Exception e)
        {
            ReportFailure(e);
        }
        finally
        {
            _savingAll = false;
            NotifySaveCommands();
        }
        RaiseNotices();
        return savedAll;
    }

    /// <summary>Why saving <paramref name="image"/>'s Result failed, as a notice.</summary>
    private static Message SaveFailedNotice(WorkspaceImage image, Exception failure) =>
        new("SaveFailed", StripEntry.DisplayName(image),
            failure is SaveFailedException saveFailure ? new SaveFailure(saveFailure) : failure.Message);

    /// <summary>Why a save failed, in the current language when shown.</summary>
    private sealed record SaveFailure(SaveFailedException Failure)
    {
        public override string ToString() => Failure.Reason switch
        {
            SaveFailureReason.TargetIsFolder => Localizer.Instance.Format("SaveFailureTargetIsFolder", Failure.TargetPath),
            SaveFailureReason.TargetExists => Localizer.Instance.Format("SaveFailureTargetExists", Failure.TargetPath),
            SaveFailureReason.FolderNotFound => Localizer.Instance.Format("SaveFailureFolderNotFound", Failure.TargetPath),
            SaveFailureReason.AccessDenied => Localizer.Instance.Format("SaveFailureAccessDenied", Failure.TargetPath),
            _ => Failure.InnerException?.Message ?? Failure.Message,
        };
    }
}
