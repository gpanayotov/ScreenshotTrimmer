using Avalonia.Controls;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.App;

/// <summary>Edits the settings; closes with the edited settings on Save, or with <c>null</c> on Cancel.</summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsWindowViewModel? _viewModel;

    // For the XAML loader and previewer.
    public SettingsWindow()
    {
        InitializeComponent();
    }

    public SettingsWindow(UserSettings settings) : this()
    {
        _viewModel = new SettingsWindowViewModel(settings);
        DataContext = _viewModel;
        SaveButton.Click += (_, _) =>
        {
            if (_viewModel.CanSave)
                Close(_viewModel.ToSettings());
        };
        CancelButton.Click += (_, _) => Close(null);
        Closed += (_, _) => _viewModel.Dispose();
    }
}
