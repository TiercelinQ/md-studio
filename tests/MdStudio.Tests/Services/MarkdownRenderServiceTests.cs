using MdStudio.Services;
using NSubstitute;

namespace MdStudio.Tests.Services;

public sealed class MarkdownRenderServiceTests
{
    private readonly MarkdownRenderService _service;

    public MarkdownRenderServiceTests()
    {
        var assets = Substitute.For<IPageAssetProvider>();
        assets.GetFileUrl(Arg.Any<string>()).Returns(call => "https://mdstudio.local/file?p=" + Uri.EscapeDataString(call.Arg<string>()));
        _service = new MarkdownRenderService(assets);
    }

    [Fact]
    public void Decoupe_le_texte_en_blocs_qui_le_recouvrent_entierement()
    {
        const string text = "\n# Titre\n\nParagraphe\nsur deux lignes.\n\n- a\n- b\n";

        var rendered = _service.Render(text, "C:\\docs", TestContext.Current.CancellationToken);

        Assert.Equal(3, rendered.Blocks.Count);
        var rebuilt = rendered.Leading + string.Concat(rendered.Blocks.Select(b => string.Concat(text.AsSpan(b.Start, b.Length), b.Separator)));
        Assert.Equal(text, rebuilt);
        Assert.Equal("# Titre", text.Substring(rendered.Blocks[0].Start, rendered.Blocks[0].Length));
    }

    [Fact]
    public void Enveloppe_chaque_bloc_avec_son_index_source()
    {
        var rendered = _service.Render("# A\n\nB\n", "C:\\docs", TestContext.Current.CancellationToken);

        Assert.Contains("data-src=\"0\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("data-src=\"1\"", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Rend_les_titres_de_niveau_1_a_6()
    {
        var rendered = _service.Render("# 1\n## 2\n### 3\n#### 4\n##### 5\n###### 6\n", "C:\\docs", TestContext.Current.CancellationToken);

        for (var level = 1; level <= 6; level++)
        {
            Assert.Contains($"<h{level}", rendered.Html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Conserve_les_blocs_mermaid_en_code_marque()
    {
        var rendered = _service.Render("```mermaid\nflowchart TD\n  A --> B\n```\n", "C:\\docs", TestContext.Current.CancellationToken);

        Assert.Contains("language-mermaid", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Retire_les_balises_script_du_html_brut()
    {
        var rendered = _service.Render("Texte\n\n<script>alert(1)</script>\n\n<img src=x onerror=alert(2)>\n", "C:\\docs", TestContext.Current.CancellationToken);

        Assert.DoesNotContain("<script", rendered.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", rendered.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sert_les_images_locales_par_l_hote_virtuel_et_garde_le_chemin_markdown()
    {
        var rendered = _service.Render("![logo](img/logo.png)\n", "C:\\docs", TestContext.Current.CancellationToken);

        Assert.Contains("https://mdstudio.local/file?p=C%3A%5Cdocs%5Cimg%5Clogo.png", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("data-md-src=\"img/logo.png\"", rendered.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Garde_les_cases_de_taches()
    {
        var rendered = _service.Render("- [x] fait\n- [ ] a faire\n", "C:\\docs", TestContext.Current.CancellationToken);

        Assert.Contains("type=\"checkbox\"", rendered.Html, StringComparison.Ordinal);
        Assert.Contains("checked", rendered.Html, StringComparison.Ordinal);
    }
}
