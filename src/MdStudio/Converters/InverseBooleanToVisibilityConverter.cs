using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MdStudio.Converters;

/// <summary>Converts <c>true</c> to <see cref="Visibility.Collapsed"/> and <c>false</c> to <see cref="Visibility.Visible"/>.</summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
