using MdReader.Helpers;

namespace MdReader.Tests.Helpers;

public sealed class FrenchPluralTests
{
    [Theory]
    [InlineData(0, "0 mot")]
    [InlineData(1, "1 mot")]
    [InlineData(2, "2 mots")]
    public void Accorde_le_nombre_de_mots(int count, string expected) => Assert.Equal(expected, FrenchPlural.Words(count));

    [Theory]
    [InlineData(1, "1 fichier Markdown")]
    [InlineData(8, "8 fichiers Markdown")]
    public void Accorde_le_nombre_de_fichiers(int count, string expected) => Assert.Equal(expected, FrenchPlural.MarkdownFiles(count));
}
