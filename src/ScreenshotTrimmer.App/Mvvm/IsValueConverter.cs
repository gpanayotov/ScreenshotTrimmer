using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace ScreenshotTrimmer.App.Mvvm;

/// <summary>
/// Binds a radio choice to one value of a property: checked while the property equals the converter parameter, and
/// checking it sets the property to the parameter. Unchecking changes nothing, since another choice is being checked.
/// </summary>
public sealed class IsValueConverter : IValueConverter
{
    public static IsValueConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : BindingOperations.DoNothing;
}
