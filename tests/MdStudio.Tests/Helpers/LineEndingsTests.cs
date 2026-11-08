using MdStudio.Helpers;
using MdStudio.Models;

namespace MdStudio.Tests.Helpers;

public sealed class LineEndingsTests
{
    [Fact]
    public void Normalise_toutes_les_fins_de_ligne() =>
        Assert.Equal("a\r\nb\r\nc\r\n", LineEndings.Normalize("a\nb\r\nc\r", LineEnding.CrLf));

    [Fact]
    public void Donne_les_caracteres_de_fin_de_ligne()
    {
        Assert.Equal("\r\n", LineEndings.ToNewLine(LineEnding.CrLf));
        Assert.Equal("\n", LineEndings.ToNewLine(LineEnding.Lf));
    }
}
