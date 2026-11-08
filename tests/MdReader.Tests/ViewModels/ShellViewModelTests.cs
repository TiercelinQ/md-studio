using System.IO;
using MdReader.Models;
using MdReader.Services;
using MdReader.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MdReader.Tests.ViewModels;

public sealed class ShellViewModelTests
{
    private readonly ITextFileService _files = Substitute.For<ITextFileService>();
    private readonly IMarkdownRenderService _renderer = Substitute.For<IMarkdownRenderService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IFeedbackService _feedback = Substitute.For<IFeedbackService>();
    private readonly IPreferencesService _preferences = Substitute.For<IPreferencesService>();
    private readonly IAppThemeService _theme = Substitute.For<IAppThemeService>();
    private readonly IFileWatchService _watcher = Substitute.For<IFileWatchService>();
    private readonly ShellViewModel _shell;

    public ShellViewModelTests()
    {
        _preferences.Current.Returns(new Preferences());
        _theme.PageTheme.Returns(new PageTheme(false, new Dictionary<string, string>()));
        _files.GetSize(Arg.Any<string>()).Returns(Result<long>.Success(10));
        _files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Result<TextFile>.Success(new TextFile(call.Arg<string>(), "texte", new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), DateTime.UtcNow, 5)));
        _renderer.RenderAsync(Arg.Any<TextFile>(), Arg.Any<CancellationToken>()).Returns(new RenderedDocument("<p>texte</p>", string.Empty, []));

        var explorer = new ExplorerViewModel(Substitute.For<IFolderScanService>(), _watcher, _feedback, NullLogger<ExplorerViewModel>.Instance);
        _shell = new ShellViewModel(
            explorer,
            () => new DocumentViewModel(_files, _renderer, new MarkdownComposeService(), Substitute.For<IExternalLauncher>(), _feedback, NullLogger<DocumentViewModel>.Instance),
            () => new LinkDialogViewModel(),
            _files,
            _renderer,
            _dialogs,
            _feedback,
            _preferences,
            _theme,
            _watcher,
            Substitute.For<IPageAssetProvider>(),
            Substitute.For<IAppPaths>(),
            NullLogger<ShellViewModel>.Instance);
    }

    [Fact]
    public async Task Ouvre_un_seul_onglet_pour_un_meme_fichier()
    {
        await _shell.OpenPathAsync(Doc("a.md"), TestContext.Current.CancellationToken);
        await _shell.OpenPathAsync(Doc("a.md"), TestContext.Current.CancellationToken);

        Assert.Single(_shell.Documents);
        Assert.Same(_shell.Documents[0], _shell.ActiveDocument);
    }

    [Fact]
    public async Task Active_l_onglet_de_droite_puis_celui_de_gauche_a_la_fermeture()
    {
        await OpenAsync("a.md", "b.md", "c.md");
        var (a, b, c) = (_shell.Documents[0], _shell.Documents[1], _shell.Documents[2]);

        _shell.ActiveDocument = b;
        await _shell.TryCloseAsync(b, TestContext.Current.CancellationToken);
        Assert.Same(c, _shell.ActiveDocument);

        await _shell.TryCloseAsync(c, TestContext.Current.CancellationToken);
        Assert.Same(a, _shell.ActiveDocument);

        await _shell.TryCloseAsync(a, TestContext.Current.CancellationToken);
        Assert.Null(_shell.ActiveDocument);
        Assert.True(_shell.HasNoDocument);
    }

    [Fact]
    public async Task Garde_l_onglet_modifie_quand_l_utilisateur_annule()
    {
        await OpenAsync("a.md");
        var document = _shell.Documents[0];
        MakeModified(document);
        _dialogs.ConfirmSaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SaveDecision.Cancel);

        var closed = await _shell.TryCloseAsync(document, TestContext.Current.CancellationToken);

        Assert.False(closed);
        Assert.Single(_shell.Documents);
    }

    [Fact]
    public async Task Ferme_l_onglet_modifie_sans_ecrire_sur_ne_pas_enregistrer()
    {
        await OpenAsync("a.md");
        var document = _shell.Documents[0];
        MakeModified(document);
        _dialogs.ConfirmSaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SaveDecision.Discard);

        var closed = await _shell.TryCloseAsync(document, TestContext.Current.CancellationToken);

        Assert.True(closed);
        Assert.Empty(_shell.Documents);
        await _files.DidNotReceive().WriteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TextFormat>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Interrompt_la_fermeture_de_la_fenetre_sur_annuler()
    {
        await OpenAsync("a.md");
        MakeModified(_shell.Documents[0]);
        _dialogs.ConfirmSaveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(SaveDecision.Cancel);

        Assert.False(await _shell.RequestWindowCloseAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Borne_le_zoom_entre_70_et_200_pour_cent()
    {
        for (var i = 0; i < 20; i++)
        {
            await _shell.ZoomInCommand.ExecuteAsync(null);
        }

        Assert.Equal(AppConfig.ZoomMax, _shell.Zoom);

        for (var i = 0; i < 20; i++)
        {
            await _shell.ZoomOutCommand.ExecuteAsync(null);
        }

        Assert.Equal(AppConfig.ZoomMin, _shell.Zoom);

        await _shell.ZoomResetCommand.ExecuteAsync(null);
        Assert.Equal("100 %", _shell.ZoomText);
    }

    [Fact]
    public async Task Conserve_le_mode_au_changement_d_onglet()
    {
        await OpenAsync("a.md", "b.md");

        Assert.True(_shell.HandleShortcut("E", control: true, shift: false));
        _shell.ActiveDocument = _shell.Documents[0];

        Assert.Equal(DocumentMode.Editing, _shell.Mode);
        Assert.True(_shell.IsFormatBarVisible);
    }

    [Fact]
    public async Task Rend_les_volets_exclusifs_en_fenetre_etroite()
    {
        _shell.UpdateWidth(1200);
        _shell.UpdateWidth(800);
        Assert.False(_shell.IsExplorerVisible);

        await _shell.ToggleExplorerCommand.ExecuteAsync(null);
        await _shell.ToggleOutlineCommand.ExecuteAsync(null);

        Assert.True(_shell.IsOutlineOverlay);
        Assert.False(_shell.IsExplorerVisible);
    }

    [Fact]
    public async Task Signale_un_fichier_illisible_sans_ouvrir_d_onglet()
    {
        _files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<TextFile>.Failure(new ResultError(FeedbackType.Danger, "illisible")));

        await _shell.OpenPathAsync(Doc("x.md"), TestContext.Current.CancellationToken);

        Assert.Empty(_shell.Documents);
        Assert.Null(_shell.ActiveDocument);
        _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Danger));
    }

    [Fact]
    public async Task Affiche_l_onglet_en_chargement_pendant_la_lecture_du_fichier()
    {
        var reading = new TaskCompletionSource<Result<TextFile>>();
        _files.ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(reading.Task);

        var opening = _shell.OpenPathAsync(Doc("gros.md"), TestContext.Current.CancellationToken);

        var document = Assert.Single(_shell.Documents);
        Assert.Same(document, _shell.ActiveDocument);
        Assert.Equal("gros.md", document.FileName);
        Assert.True(document.IsLoading);

        reading.SetResult(Result<TextFile>.Success(new TextFile(document.FilePath, "texte", new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), DateTime.UtcNow, 5)));
        await opening;

        Assert.Equal("texte", document.SavedText);
    }

    [Fact]
    public async Task Passe_en_mode_source_avec_ctrl_u()
    {
        await OpenAsync("a.md");

        Assert.True(_shell.HandleShortcut("U", control: true, shift: false));

        Assert.Equal(DocumentMode.Source, _shell.Mode);
        Assert.True(_shell.IsSource);
        Assert.False(_shell.IsFormatBarVisible);
        Assert.Equal("Source", _shell.ModeText);
    }

    [Fact]
    public async Task N_ouvre_rien_quand_la_creation_est_annulee()
    {
        _dialogs.PickNewMarkdownFile(Arg.Any<string?>()).Returns((string?)null);

        await _shell.CreateFileCommand.ExecuteAsync(null);

        Assert.Empty(_shell.Documents);
        await _files.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ouvre_le_fichier_cree_en_mode_edition()
    {
        var path = Doc("nouveau-" + Guid.NewGuid().ToString("N") + ".md");
        _dialogs.PickNewMarkdownFile(Arg.Any<string?>()).Returns(path);
        _files.CreateAsync(path, Arg.Any<CancellationToken>())
            .Returns(Result<TextFile>.Success(new TextFile(path, string.Empty, new TextFormat(TextEncodingKind.Utf8, LineEnding.CrLf), DateTime.UtcNow, 0)));

        await _shell.CreateFileCommand.ExecuteAsync(null);

        Assert.Equal(path, Assert.Single(_shell.Documents).FilePath);
        Assert.Equal(DocumentMode.Editing, _shell.Mode);
    }

    [Fact]
    public async Task Ouvre_un_fichier_existant_sans_le_recreer()
    {
        var folder = Path.Combine(Path.GetTempPath(), "mdreader-create-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "existant.md");
            await File.WriteAllTextAsync(path, "# Existant", TestContext.Current.CancellationToken);
            _dialogs.PickNewMarkdownFile(Arg.Any<string?>()).Returns(path);

            await _shell.CreateFileCommand.ExecuteAsync(null);

            await _files.DidNotReceive().CreateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
            Assert.Single(_shell.Documents);
            Assert.Equal(DocumentMode.Reading, _shell.Mode);
            _feedback.Received(1).Show(Arg.Is<ResultError>(e => e.Type == FeedbackType.Info));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static string Doc(string name) => Path.Combine(Path.GetTempPath(), "mdreader-shell", name);

    private static void MakeModified(DocumentViewModel document) =>
        document.HandlePageMessage(new PageMessage { Type = "changed", Blocks = [new PageBlock { Md = "autre texte" }] });

    private async Task OpenAsync(params string[] names)
    {
        foreach (var name in names)
        {
            await _shell.OpenPathAsync(Doc(name), TestContext.Current.CancellationToken);
        }
    }
}
