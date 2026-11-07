using MdReader.Models;
using MdReader.Services;
using MdReader.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MdReader.Tests.ViewModels;

public sealed class ExplorerViewModelTests : IDisposable
{
    private readonly IFolderScanService _scanner = Substitute.For<IFolderScanService>();
    private readonly ExplorerViewModel _explorer;

    public ExplorerViewModelTests()
    {
        var tree = Folder("docs", "C:\\docs",
            Folder("architecture", "C:\\docs\\architecture",
                File("flux-paiement.md", "C:\\docs\\architecture\\flux-paiement.md"),
                File("vue-ensemble.md", "C:\\docs\\architecture\\vue-ensemble.md")),
            Folder("guides", "C:\\docs\\guides",
                File("demarrage.md", "C:\\docs\\guides\\demarrage.md")));
        _scanner.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result<FileNode>.Success(tree));
        _explorer = new ExplorerViewModel(_scanner, Substitute.For<IFileWatchService>(), Substitute.For<IFeedbackService>(), NullLogger<ExplorerViewModel>.Instance);
    }

    public void Dispose() => _explorer.Dispose();

    [Fact]
    public async Task Filtre_les_fichiers_et_garde_leurs_dossiers_parents()
    {
        await _explorer.LoadAsync("C:\\docs", TestContext.Current.CancellationToken);
        var guides = _explorer.Nodes[1];

        _explorer.FilterText = "FLUX";

        var visibleFiles = _explorer.Nodes.SelectMany(n => n.SelfAndDescendants()).Where(n => n.IsVisible && !n.IsFolder).Select(n => n.Name);
        Assert.Equal(["flux-paiement.md"], visibleFiles);
        Assert.True(_explorer.Nodes[0].IsOpen);
        Assert.False(guides.IsVisible);
    }

    [Fact]
    public async Task Signale_l_absence_de_resultat_puis_restaure_l_arbre()
    {
        await _explorer.LoadAsync("C:\\docs", TestContext.Current.CancellationToken);
        var guides = _explorer.Nodes[1];
        guides.IsOpen = false;

        _explorer.FilterText = "zzz";
        Assert.True(_explorer.HasNoMatch);

        _explorer.FilterText = string.Empty;
        Assert.False(_explorer.HasNoMatch);
        Assert.True(guides.IsVisible);
        Assert.False(guides.IsOpen);
    }

    [Fact]
    public async Task Compte_les_fichiers_au_pluriel_et_au_singulier()
    {
        await _explorer.LoadAsync("C:\\docs", TestContext.Current.CancellationToken);
        Assert.Equal("3 fichiers Markdown", _explorer.FooterText);

        _scanner.ScanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<FileNode>.Success(Folder("seul", "C:\\seul", File("a.md", "C:\\seul\\a.md"))));
        await _explorer.LoadAsync("C:\\seul", TestContext.Current.CancellationToken);
        Assert.Equal("1 fichier Markdown", _explorer.FooterText);
    }

    [Fact]
    public async Task Demande_l_ouverture_d_un_fichier_et_deplie_un_dossier()
    {
        await _explorer.LoadAsync("C:\\docs", TestContext.Current.CancellationToken);
        string? opened = null;
        _explorer.FileOpenRequested += (_, path) => opened = path;
        var guides = _explorer.Nodes[1];
        var before = guides.IsOpen;

        _explorer.ActivateCommand.Execute(guides);
        _explorer.ActivateCommand.Execute(guides.Children[0]);

        Assert.NotEqual(before, guides.IsOpen);
        Assert.Equal("C:\\docs\\guides\\demarrage.md", opened);
    }

    private static FileNode Folder(string name, string path, params FileNode[] children) => new(name, path, true, children);

    private static FileNode File(string name, string path) => new(name, path, false, []);
}
