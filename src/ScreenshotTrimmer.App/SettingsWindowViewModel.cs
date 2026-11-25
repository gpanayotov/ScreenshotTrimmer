using System.ComponentModel;
using ScreenshotTrimmer.App.Localization;
using ScreenshotTrimmer.App.Mvvm;
using ScreenshotTrimmer.Processing.Files;
using ScreenshotTrimmer.Processing.Settings;

namespace ScreenshotTrimmer.App;

/// <summary>
/// The settings window's edits: language, save mode and both suffixes. Suffixes are checked with the library's own
/// rule, and the settings can be saved only while both are valid.
/// </summary>
public sealed class SettingsWindowViewModel : Observable, IDisposable
{
    private readonly UserSettings _original;
    private Language _language;
    private bool _isBackupMode;
    private string _resultSuffix;
    private string _backupSuffix;

    public SettingsWindowViewModel(UserSettings settings)
    {
        _original = settings;
        _language = settings.Language;
        _isBackupMode = settings.SaveMode == SaveMode.Backup;
        _resultSuffix = settings.ResultSuffix;
        _backupSuffix = settings.BackupSuffix;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
    }

    /// <summary>The choices for <see cref="SelectedLanguageIndex"/>, in order.</summary>
    public static IReadOnlyList<Language> Languages { get; } = [Language.System, Language.En, Language.Bg];

    public int SelectedLanguageIndex
    {
        get => IndexOf(_language);
        set
        {
            if (value >= 0 && value < Languages.Count && Languages[value] != _language)
            {
                _language = Languages[value];
                Raise();
            }
        }
    }

    public bool IsSuffixMode
    {
        get => !_isBackupMode;
        set => IsBackupMode = !value;
    }

    public bool IsBackupMode
    {
        get => _isBackupMode;
        set
        {
            if (Set(ref _isBackupMode, value))
                Raise(nameof(IsSuffixMode));
        }
    }

    public string ResultSuffix
    {
        get => _resultSuffix;
        set
        {
            if (Set(ref _resultSuffix, value ?? ""))
                RaiseValidation();
        }
    }

    public string BackupSuffix
    {
        get => _backupSuffix;
        set
        {
            if (Set(ref _backupSuffix, value ?? ""))
                RaiseValidation();
        }
    }

    /// <summary>Why the Result suffix cannot be used, or <c>null</c> when it can.</summary>
    public string? ResultSuffixError => SuffixError(_resultSuffix);

    /// <summary>Why the backup suffix cannot be used, or <c>null</c> when it can.</summary>
    public string? BackupSuffixError => SuffixError(_backupSuffix);

    public bool CanSave => FileSuffix.IsValid(_resultSuffix) && FileSuffix.IsValid(_backupSuffix);

    /// <summary>The edited settings; other settings keep the values they had.</summary>
    public UserSettings ToSettings() => _original with
    {
        Language = _language,
        SaveMode = _isBackupMode ? SaveMode.Backup : SaveMode.Suffix,
        ResultSuffix = _resultSuffix,
        BackupSuffix = _backupSuffix,
    };

    public void Dispose() => Localizer.Instance.PropertyChanged -= OnLanguageChanged;

    private static int IndexOf(Language language)
    {
        for (var i = 0; i < Languages.Count; i++)
        {
            if (Languages[i] == language)
                return i;
        }
        return 0;
    }

    private static string? SuffixError(string suffix) =>
        FileSuffix.IsValid(suffix)
            ? null
            : Localizer.Instance[string.IsNullOrEmpty(suffix) ? "SuffixEmpty" : "SuffixInvalid"];

    private void RaiseValidation()
    {
        Raise(nameof(ResultSuffixError));
        Raise(nameof(BackupSuffixError));
        Raise(nameof(CanSave));
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        Raise(nameof(ResultSuffixError));
        Raise(nameof(BackupSuffixError));
    }
}
