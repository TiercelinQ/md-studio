using System.Globalization;
using System.Windows;
using MdStudio.Converters;

namespace MdStudio.Tests.Converters;

public sealed class DepthToIndentConverterTests
{
    [Fact]
    public void Calcule_le_retrait_par_niveau()
    {
        var converter = new DepthToIndentConverter { BaseIndent = 6, IndentPerLevel = 16, RightPadding = 8 };

        Assert.Equal(new Thickness(38, 0, 8, 0), converter.Convert(2, typeof(Thickness), null!, CultureInfo.InvariantCulture));
    }
}
