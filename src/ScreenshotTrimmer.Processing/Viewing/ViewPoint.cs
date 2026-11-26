namespace ScreenshotTrimmer.Processing.Viewing;

/// <summary>
/// A point on screen in a viewer, in device-independent units from the view's top-left corner, as pointer events give
/// it. The display scaling turns it into the physical pixels a <see cref="Viewport"/> works in.
/// </summary>
public readonly record struct ViewPoint(double X, double Y);
