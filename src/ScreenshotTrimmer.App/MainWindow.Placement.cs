using Avalonia;
using Avalonia.Controls;
using ScreenshotTrimmer.Processing.Settings;
using ScreenRect = ScreenshotTrimmer.Processing.Imaging.PixelRect;

namespace ScreenshotTrimmer.App;

/// <summary>Restores the window's size, position, maximised state and strip width, and saves them on close.</summary>
public partial class MainWindow
{
    private PixelPoint _normalPosition;
    private Size _normalSize;

    private ColumnDefinition StripColumn => Panes.ColumnDefinitions[0];

    /// <summary>
    /// Lays the window out as <paramref name="store"/> remembers it, kept on a visible screen, and saves the layout
    /// there when the window closes. Call before the window is shown. With nothing usable saved, the window keeps its
    /// default layout.
    /// </summary>
    public void RememberPlacement(WindowPlacementStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _normalSize = new Size(Width, Height);
        if (store.Load() is { } placement)
            Restore(placement);

        // While maximised, the window reports the maximised geometry; keep the last normal one to save.
        PositionChanged += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
                _normalPosition = e.Point;
        };
        Resized += (_, e) =>
        {
            if (WindowState == WindowState.Normal)
                _normalSize = e.ClientSize;
        };
        Closing += (_, _) => SavePlacement(store);
    }

    private void Restore(WindowPlacement placement)
    {
        var scaling = (Screens.ScreenFromPoint(new PixelPoint(placement.X, placement.Y)) ?? Screens.Primary)?.Scaling ?? 1.0;
        var saved = new ScreenRect(
            placement.X,
            placement.Y,
            (int)Math.Round(placement.Width * scaling),
            (int)Math.Round(placement.Height * scaling));
        var workingAreas = Screens.All
            .Select(screen => new ScreenRect(screen.WorkingArea.X, screen.WorkingArea.Y, screen.WorkingArea.Width, screen.WorkingArea.Height))
            .ToList();
        var fitted = ScreenFit.Place(saved, workingAreas);

        WindowStartupLocation = WindowStartupLocation.Manual;
        _normalPosition = new PixelPoint(fitted.X, fitted.Y);
        _normalSize = new Size(
            fitted.Width == saved.Width ? placement.Width : fitted.Width / scaling,
            fitted.Height == saved.Height ? placement.Height : fitted.Height / scaling);
        Position = _normalPosition;
        Width = _normalSize.Width;
        Height = _normalSize.Height;
        StripColumn.Width = new GridLength(Math.Clamp(placement.StripWidth, StripColumn.MinWidth, StripColumn.MaxWidth));
        if (placement.Maximized)
            WindowState = WindowState.Maximized;
    }

    private void SavePlacement(WindowPlacementStore store)
    {
        try
        {
            store.Save(new WindowPlacement
            {
                X = _normalPosition.X,
                Y = _normalPosition.Y,
                Width = _normalSize.Width,
                Height = _normalSize.Height,
                Maximized = WindowState == WindowState.Maximized,
                StripWidth = StripColumn.ActualWidth,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The layout is a convenience; failing to remember it must not stop the window from closing.
        }
    }
}
