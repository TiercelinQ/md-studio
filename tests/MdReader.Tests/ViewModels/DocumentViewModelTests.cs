using System.IO;
using MdReader.Models;
using MdReader.Services;
using MdReader.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MdReader.Tests.ViewModels;

public sealed class DocumentViewModelTests : IDisposable
{
    private const string Source = "# Titre\n\nTexte.\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mdreader-doc-" + Guid.NewGuid().ToString("N"));
    private readonly ITextFileService _files = Substitute.For<ITextFileService>();
    private readonly IExternalLauncher _launcher = Substitute.For<IExternalLauncher>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IDocumentSurface _surface = Substitute.For<IDocumentSurface>();
    private readonly DocumentViewModel _document;

    public DocumentViewModelTests()
    {
        Directory.CreateDirectory(_folder);
        var renderer = new MarkdownRenderService(Substitute.For<IPageAssetProvider>());
        _document = new DocumentViewModel(_files, renderer, new MarkdownComposeService(), _launcher, _feedback, NullLogger<DocumentViewModel>.Instance);
        var path = Path.Combine(_folder, "a.md");
        var file = new TextFile(path, Source, new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), DateTime.UtcNow, Source.Length);
        _document.Initialize(file, renderer.Render(Source, _folder, CancellationToken.None), new DocumentPageState(DocumentMode.Reading, 1, new PageTheme(false, new Dictionary<string, string>())), _folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void N_est_modifie_que_si_le_contenu_differe_du_fichier()
    {
        _document.HandlePageMessage(Changed(new PageBlock { Src = 0 }, new PageBlock { Md = "Texte modifié." }));
        Assert.True(_document.IsModified);

        _document.HandlePageMessage(Changed(new PageBlock { Src = 0 }, new PageBlock { Src = 1 }));
        Assert.False(_document.IsModified);
    }

    [Fact]
    public async Task Reste_modifie_et_signale_l_erreur_quand_l_ecriture_echoue()
    {
        await _document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);
        _surface.SerializeAsync(Arg.Any<CancellationToken>()).Returns([new EditedBlock(0, null), new EditedBlock(null, "Autre.")]);
        _files.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextFormat>(), Arg.Any<CancellationToken>())
            .Returns(Result<TextFile>.Failure(new ResultError(FeedbackType.Danger, "Impossible d'enregistrer")));
        _document.HandlePageMessage(Changed(new PageBlock { Src = 0 }, new PageBlock { Md = "Autre." }));

        var saved = await _document.SaveAsync(TestContext.Current.CancellationToken);

        Assert.False(saved);
        Assert.True(_document.IsModified);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Danger));
    }

    [Fact]
    public async Task Enregistre_le_texte_recompose_et_retire_l_etat_modifie()
    {
        await _document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);
        _surface.SerializeAsync(Arg.Any<CancellationToken>()).Returns([new EditedBlock(0, null), new EditedBlock(null, "Autre.")]);
        _files.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextFormat>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<TextFile>.Success(new TextFile(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<TextFormat>(2), DateTime.UtcNow, 1)));
        _document.HandlePageMessage(Changed(new PageBlock { Src = 0 }, new PageBlock { Md = "Autre." }));

        Assert.True(await _document.SaveAsync(TestContext.Current.CancellationToken));

        Assert.False(_document.IsModified);
        await _files.Received(1).WriteAsync(_document.FilePath, "# Titre\n\nAutre.\n", Arg.Any<TextFormat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Ouvre_un_lien_externe_dans_le_navigateur()
    {
        _launcher.Open(Arg.Any<string>()).Returns(Result<Unit>.Success(Unit.Value));

        _document.HandlePageMessage(new PageMessage { Type = "link", Href = "https://exemple.fr" });

        _launcher.Received(1).Open("https://exemple.fr");
    }

    [Fact]
    public void Demande_l_ouverture_d_un_lien_markdown_existant()
    {
        File.WriteAllText(Path.Combine(_folder, "b.md"), "# B");
        string? requested = null;
        _document.OpenFileRequested += (_, path) => requested = path;

        _document.HandlePageMessage(new PageMessage { Type = "link", Href = "b.md#section" });

        Assert.Equal(Path.Combine(_folder, "b.md"), requested);
    }

    [Fact]
    public void Signale_un_lien_markdown_introuvable()
    {
        _document.HandlePageMessage(new PageMessage { Type = "link", Href = "absent.md" });

        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Danger));
    }

    [Fact]
    public void Affiche_le_compteur_de_recherche()
    {
        _document.HandlePageMessage(new PageMessage { Type = "searchResult", Current = 2, Total = 5 });
        Assert.Equal("2 sur 5", _document.SearchCounterText);

        _document.HandlePageMessage(new PageMessage { Type = "searchResult", Current = 0, Total = 0 });
        Assert.Equal("Aucun résultat", _document.SearchCounterText);
    }

    private static PageMessage Changed(params PageBlock[] blocks) => new() { Type = "changed", Blocks = blocks };
}
