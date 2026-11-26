using System.Windows.Input;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Workspaces;

namespace ScreenshotTrimmer.App;

// Reviewing a Batch: jumping between the images with no Subject found or with unsaved Steps, and the strip filter that
// shows only the images with no Subject found.
public sealed partial class MainWindowViewModel
{
    private bool _showOnlyNoSubjectFound;
    private bool _offersShowNoSubjectFound;
    private ICommand? _nextNoSubjectFoundCommand;
    private ICommand? _previousNoSubjectFoundCommand;
    private ICommand? _nextUnsavedCommand;
    private ICommand? _previousUnsavedCommand;
    private ICommand? _toggleShowOnlyNoSubjectFoundCommand;
    private ICommand? _showNoSubjectFoundCommand;

    /// <summary>Selects the next image with no Subject found; nothing happens when none follows.</summary>
    public ICommand NextNoSubjectFoundCommand =>
        _nextNoSubjectFoundCommand ??= new RelayCommand(() => _workspace.Next(image => image.NoSubjectFound));

    /// <summary>Selects the previous image with no Subject found; nothing happens when none comes before.</summary>
    public ICommand PreviousNoSubjectFoundCommand =>
        _previousNoSubjectFoundCommand ??= new RelayCommand(() => _workspace.Previous(image => image.NoSubjectFound));

    /// <summary>Selects the next image with unsaved Steps; nothing happens when none follows.</summary>
    public ICommand NextUnsavedCommand =>
        _nextUnsavedCommand ??= new RelayCommand(() => _workspace.Next(image => image.HasUnsavedSteps));

    /// <summary>Selects the previous image with unsaved Steps; nothing happens when none comes before.</summary>
    public ICommand PreviousUnsavedCommand =>
        _previousUnsavedCommand ??= new RelayCommand(() => _workspace.Previous(image => image.HasUnsavedSteps));

    /// <summary>Turns <see cref="ShowOnlyNoSubjectFound"/> on or off.</summary>
    public ICommand ToggleShowOnlyNoSubjectFoundCommand =>
        _toggleShowOnlyNoSubjectFoundCommand ??= new RelayCommand(() => ShowOnlyNoSubjectFound = !ShowOnlyNoSubjectFound);

    /// <summary>The Batch summary's "Show" action: turns <see cref="ShowOnlyNoSubjectFound"/> on.</summary>
    public ICommand ShowNoSubjectFoundCommand =>
        _showNoSubjectFoundCommand ??= new RelayCommand(() => ShowOnlyNoSubjectFound = true);

    /// <summary>
    /// Whether the strip shows only the images with no Subject found, and the selected image, so one just fixed stays
    /// in view until the selection moves on. Next and previous then move between the images the strip shows. Off by
    /// default and whenever another list is opened.
    /// </summary>
    public bool ShowOnlyNoSubjectFound
    {
        get => _showOnlyNoSubjectFound;
        set
        {
            if (!Set(ref _showOnlyNoSubjectFound, value))
                return;
            // A selected image the filter hides gives way to the first one it shows.
            if (value && _workspace.Selected is { NoSubjectFound: false } && _listedImages?.Any(image => image.NoSubjectFound) == true)
            {
                _workspace.Next(image => image.NoSubjectFound);
                if (_workspace.Selected is { NoSubjectFound: false })
                    _workspace.Previous(image => image.NoSubjectFound);
            }
            ShowInStrip(_listedImages ?? [], _workspace.Selected);
            Set(ref _selectedEntry, Entries.FirstOrDefault(entry => entry.Image.Key == _workspace.Selected?.Key),
                nameof(SelectedEntry));
        }
    }

    /// <summary>Whether the notices include a Batch summary that found images with no Subject, so "Show" is offered.</summary>
    public bool OffersShowNoSubjectFound => _offersShowNoSubjectFound;

    /// <summary>Offers, or stops offering, the notices' "Show" action.</summary>
    private void OfferShowNoSubjectFound(bool offer)
    {
        _offersShowNoSubjectFound = offer;
        Raise(nameof(OffersShowNoSubjectFound));
    }

    /// <summary>Whether the strip shows <paramref name="image"/>, given that <paramref name="selected"/> is selected.</summary>
    private bool InStrip(WorkspaceImage image, WorkspaceImage? selected) =>
        !ShowOnlyNoSubjectFound || image.NoSubjectFound || image.Key == selected?.Key;

    /// <summary>Selects the next image the strip shows.</summary>
    private void Next() => _workspace.Next(image => InStrip(image, null));

    /// <summary>Selects the previous image the strip shows.</summary>
    private void Previous() => _workspace.Previous(image => InStrip(image, null));

    /// <summary>
    /// Shows <paramref name="image"/>, just selected, in the Result view when its Steps came from a Batch, so it can be
    /// judged straight away.
    /// </summary>
    private void ShowBatchResultOf(WorkspaceImage? image)
    {
        if (image is not null)
            IfStillListed(image, listed => ShowsResult |= _workspace.HasBatchSteps(listed));
    }
}
