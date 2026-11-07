using System.IO;
using MdReader.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MdReader.Tests.Services;

public sealed class FolderScanServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mdreader-scan-" + Guid.NewGuid().ToString("N"));
    private readonly FolderScanService _service = new(NullLogger<FolderScanService>.Instance);

    public FolderScanServiceTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "docs", "sous"));
        Directory.CreateDirectory(Path.Combine(_root, "vide"));
        File.WriteAllText(Path.Combine(_root, "file10.md"), "x");
        File.WriteAllText(Path.Combine(_root, "file2.MARKDOWN"), "x");
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "docs", "sous", "a.md"), "x");
        File.WriteAllText(Path.Combine(_root, "vide", "b.txt"), "x");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Ne_garde_que_les_fichiers_markdown_et_masque_les_dossiers_sans_markdown()
    {
        var result = await _service.ScanAsync(_root, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal(["docs", "file2.MARKDOWN", "file10.md"], result.Data!.Children.Select(c => c.Name));
        Assert.Equal(3, result.Data.FileCount);
    }

    [Fact]
    public async Task Echoue_proprement_sur_un_dossier_absent()
    {
        var result = await _service.ScanAsync(Path.Combine(_root, "absent"), TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Respecte_l_annulation()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _service.ScanAsync(_root, cancelled.Token));
    }
}
