using System.Globalization;
using System.Windows;
using MdReader.Converters;

namespace MdReader.Tests.Converters;

public sealed class InverseBooleanToVisibilityConverterTests
{
    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void Inverse_la_visibilite(bool value, Visibility expected) =>
        Assert.Equal(expected, new InverseBooleanToVisibilityConverter().Convert(value, typeof(Visibility), null!, CultureInfo.InvariantCulture));
}
