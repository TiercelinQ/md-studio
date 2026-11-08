using System.IO;
using MdStudio.Models;
using MdStudio.Services;
using MdStudio.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MdStudio.Tests.ViewModels;

public sealed class DocumentViewModelTests : IDisposable
{
    private const string Source = "# Titre\n\nTexte.\n";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mdstudio-doc-" + Guid.NewGuid().ToString("N"));
    private readonly ITextFileService _files = Substitute.For<ITextFileService>();
    private readonly IExternalLauncher _launcher = Substitute.For<IExternalLauncher>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IDocumentSurface _surface = Substitute.For<IDocumentSurface>();
    private readonly MarkdownRenderService _renderer = new(Substitute.For<IPageAssetProvider>());
    private readonly DocumentViewModel _document;

    public DocumentViewModelTests()
    {
        Directory.CreateDirectory(_folder);
        _document = Prepared();
        var file = new TextFile(_document.FilePath, Source, new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), DateTime.UtcNow, Source.Length);
        _document.Show(file, _renderer.Render(Source, _folder, CancellationToken.None));
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

    [Fact]
    public void Affiche_le_chargement_jusqu_au_rendu_de_la_page()
    {
        Assert.True(_document.IsLoading);

        _document.HandlePageMessage(new PageMessage { Type = "rendered" });

        Assert.False(_document.IsLoading);
    }

    [Fact]
    public async Task Envoie_le_texte_recompose_a_l_entree_en_mode_source()
    {
        await _document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);
        _surface.SerializeAsync(Arg.Any<CancellationToken>()).Returns([new EditedBlock(0, null), new EditedBlock(null, "Autre.")]);

        await _document.ApplyModeAsync(DocumentMode.Source, TestContext.Current.CancellationToken);

        _surface.Received(1).ShowSource("# Titre\n\nAutre.\n");
    }

    [Fact]
    public void Compare_le_texte_source_au_fichier_enregistre_quelles_que_soient_les_fins_de_ligne()
    {
        _document.HandlePageMessage(new PageMessage { Type = "sourceChanged", Source = "# Titre\r\n\r\nTexte.\r\n" });
        Assert.False(_document.IsModified);

        _document.HandlePageMessage(new PageMessage { Type = "sourceChanged", Source = "# Titre\n\nNouveau.\n" });
        Assert.True(_document.IsModified);
    }

    [Fact]
    public async Task Enregistre_le_texte_source_en_mode_source()
    {
        await EnterSourceModeAsync("# Titre\n\nSource.\n");
        _files.WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextFormat>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<TextFile>.Success(new TextFile(call.ArgAt<string>(0), call.ArgAt<string>(1), call.ArgAt<TextFormat>(2), DateTime.UtcNow, 1)));

        Assert.True(await _document.SaveAsync(TestContext.Current.CancellationToken));

        await _files.Received(1).WriteAsync(_document.FilePath, "# Titre\n\nSource.\n", Arg.Any<TextFormat>(), Arg.Any<CancellationToken>());
        await _surface.DidNotReceive().SerializeAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rend_de_nouveau_la_source_modifiee_au_retour_en_lecture()
    {
        await EnterSourceModeAsync("# Nouveau\n");

        await _document.ApplyModeAsync(DocumentMode.Reading, TestContext.Current.CancellationToken);

        await _surface.Received(1).LoadAsync(Arg.Is<string>(html => html.Contains("Nouveau", StringComparison.Ordinal)), Arg.Any<DocumentPageState>(), Arg.Any<CancellationToken>());
        _surface.DidNotReceive().SetMode(DocumentMode.Reading);
        Assert.True(_document.IsLoading);
    }

    [Fact]
    public async Task Repasse_en_lecture_sans_nouveau_rendu_quand_la_source_est_inchangee()
    {
        await EnterSourceModeAsync(Source);

        await _document.ApplyModeAsync(DocumentMode.Reading, TestContext.Current.CancellationToken);

        _surface.Received(1).SetMode(DocumentMode.Reading);
        await _surface.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<DocumentPageState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task N_envoie_rien_a_la_page_avant_la_fin_de_la_lecture()
    {
        var document = Prepared();

        var attach = document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);

        Assert.False(attach.IsCompleted);
        Assert.Null(document.Surface);
        Assert.True(document.IsLoading);
        await _surface.DidNotReceive().LoadAsync(Arg.Any<string>(), Arg.Any<DocumentPageState>(), Arg.Any<CancellationToken>());

        document.Show(new TextFile(document.FilePath, Source, new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), DateTime.UtcNow, Source.Length), _renderer.Render(Source, _folder, CancellationToken.None));
        await attach;

        Assert.Same(_surface, document.Surface);
        await _surface.Received(1).LoadAsync(Arg.Any<string>(), Arg.Any<DocumentPageState>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Annule_l_attachement_de_la_page_d_un_fichier_abandonne()
    {
        var document = Prepared();
        var attach = document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);

        document.Abandon();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => attach);
        Assert.Null(document.Surface);
    }

    private DocumentViewModel Prepared()
    {
        var document = new DocumentViewModel(_files, _renderer, new MarkdownComposeService(), _launcher, _feedback, NullLogger<DocumentViewModel>.Instance);
        document.Prepare(Path.Combine(_folder, "a.md"), new DocumentPageState(DocumentMode.Reading, 1, new PageTheme(false, new Dictionary<string, string>())), _folder);
        return document;
    }

    private async Task EnterSourceModeAsync(string typedSource)
    {
        await _document.AttachSurfaceAsync(_surface, TestContext.Current.CancellationToken);
        _surface.SerializeAsync(Arg.Any<CancellationToken>()).Returns([new EditedBlock(0, null), new EditedBlock(1, null)]);
        await _document.ApplyModeAsync(DocumentMode.Source, TestContext.Current.CancellationToken);
        _surface.ClearReceivedCalls();
        _surface.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(typedSource);
        _document.HandlePageMessage(new PageMessage { Type = "rendered" });
    }

    private static PageMessage Changed(params PageBlock[] blocks) => new() { Type = "changed", Blocks = blocks };
}
