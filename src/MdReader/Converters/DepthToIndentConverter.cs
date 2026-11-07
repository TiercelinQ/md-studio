using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MdReader.Converters;

/// <summary>
/// Converts a tree depth into the row padding of component C-09: left padding
/// <see cref="BaseIndent"/> + <see cref="IndentPerLevel"/> × depth, right padding <see cref="RightPadding"/>.
/// </summary>
[ValueConversion(typeof(int), typeof(Thickness))]
public sealed class DepthToIndentConverter : IValueConverter
{
    /// <summary>Gets or sets the left padding of the first level (token <c>TreeRowBaseIndent</c>).</summary>
    public double BaseIndent { get; set; }

    /// <summary>Gets or sets the indentation added per level (token <c>TreeIndentPerLevel</c>).</summary>
    public double IndentPerLevel { get; set; }

    /// <summary>Gets or sets the right padding (token <c>TreeRowRightPadding</c>).</summary>
    public double RightPadding { get; set; }

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new Thickness(BaseIndent + (IndentPerLevel * (value is int depth ? depth : 0)), 0, RightPadding, 0);

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
