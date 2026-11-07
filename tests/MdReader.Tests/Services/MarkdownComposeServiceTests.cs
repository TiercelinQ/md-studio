using MdReader.Models;
using MdReader.Services;
using NSubstitute;

namespace MdReader.Tests.Services;

public sealed class MarkdownComposeServiceTests
{
    private const string Source = "# Titre\r\n\r\nPremier   paragraphe *garde* son  style.\r\n\r\n* liste\r\n* inchangee\r\n\r\nDernier paragraphe.\r\n";

    private readonly MarkdownComposeService _composer = new();
    private readonly RenderedDocument _rendered;

    public MarkdownComposeServiceTests()
    {
        var assets = Substitute.For<IPageAssetProvider>();
        _rendered = new MarkdownRenderService(assets).Render(Source, "C:\\docs", CancellationToken.None);
    }

    [Fact]
    public void Restitue_le_texte_a_l_identique_sans_modification()
    {
        var blocks = _rendered.Blocks.Select(b => new EditedBlock(b.Index, null)).ToList();

        Assert.Equal(Source, _composer.Compose(Source, _rendered, blocks, LineEnding.CrLf));
    }

    [Fact]
    public void Ne_reecrit_que_le_paragraphe_modifie()
    {
        var blocks = new List<EditedBlock>
        {
            new(0, null),
            new(1, null),
            new(2, null),
            new(null, "Dernier paragraphe **corrige**."),
        };

        var result = _composer.Compose(Source, _rendered, blocks, LineEnding.CrLf);

        Assert.Equal(Source.Replace("Dernier paragraphe.", "Dernier paragraphe **corrige**.", StringComparison.Ordinal), result);
    }

    [Fact]
    public void Ajoute_un_bloc_avec_les_fins_de_ligne_du_fichier()
    {
        var blocks = new List<EditedBlock> { new(0, null), new(null, "Nouveau\nbloc"), new(1, null), new(2, null), new(3, null) };

        var result = _composer.Compose(Source, _rendered, blocks, LineEnding.CrLf);

        Assert.StartsWith("# Titre\r\n\r\nNouveau\r\nbloc\r\n\r\nPremier   paragraphe", result, StringComparison.Ordinal);
        Assert.EndsWith("Dernier paragraphe.\r\n", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Supprime_un_bloc_sans_toucher_aux_autres()
    {
        var blocks = new List<EditedBlock> { new(0, null), new(2, null), new(3, null) };

        var result = _composer.Compose(Source, _rendered, blocks, LineEnding.CrLf);

        Assert.Equal("# Titre\r\n\r\n* liste\r\n* inchangee\r\n\r\nDernier paragraphe.\r\n", result);
    }

    [Fact]
    public void Ignore_un_bloc_vide()
    {
        var blocks = _rendered.Blocks.Select(b => new EditedBlock(b.Index, null)).Append(new EditedBlock(null, "  \n")).ToList();

        Assert.Equal(Source, _composer.Compose(Source, _rendered, blocks, LineEnding.CrLf));
    }
}
