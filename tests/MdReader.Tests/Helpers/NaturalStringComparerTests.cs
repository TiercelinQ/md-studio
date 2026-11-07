using MdReader.Helpers;

namespace MdReader.Tests.Helpers;

public sealed class NaturalStringComparerTests
{
    private static readonly string[] Names = ["file10.md", "File2.md", "file1.md"];

    [Fact]
    public void Trie_les_nombres_dans_l_ordre_naturel_sans_tenir_compte_de_la_casse()
    {
        var sorted = Names.Order(NaturalStringComparer.Instance);

        Assert.Equal(["file1.md", "File2.md", "file10.md"], sorted);
    }

    [Fact]
    public void Place_null_en_premier() => Assert.True(NaturalStringComparer.Instance.Compare(null, "a") < 0);
}
