using System.IO;
using System.Text;
using MdReader.Models;
using MdReader.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MdReader.Tests.Services;

public sealed class TextFileServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "mdreader-tests-" + Guid.NewGuid().ToString("N"));
    private readonly TextFileService _service = new(NullLogger<TextFileService>.Instance);

    public TextFileServiceTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Detecte_utf8_avec_bom_et_fins_de_ligne_crlf()
    {
        var path = Write("a.md", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("# Titre\r\ntexte\r\n")]);

        var result = await _service.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal(new TextFormat(TextEncodingKind.Utf8Bom, LineEnding.CrLf), result.Data!.Format);
        Assert.Equal("# Titre\r\ntexte\r\n", result.Data.Text);
    }

    [Fact]
    public async Task Detecte_utf16_little_endian_et_fins_de_ligne_lf()
    {
        var path = Write("b.md", [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("é\nb\n")]);

        var result = await _service.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(new TextFormat(TextEncodingKind.Utf16Le, LineEnding.Lf), result.Data!.Format);
        Assert.Equal("é\nb\n", result.Data.Text);
    }

    [Fact]
    public async Task Refuse_un_fichier_binaire()
    {
        var path = Write("c.md", [0x41, 0x00, 0x42]);

        var result = await _service.ReadAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal(FeedbackType.Danger, result.Error!.Type);
    }

    [Fact]
    public async Task Signale_un_fichier_introuvable()
    {
        var result = await _service.ReadAsync(Path.Combine(_folder, "absent.md"), TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Ecrit_dans_le_format_d_origine()
    {
        var path = Path.Combine(_folder, "d.md");
        var format = new TextFormat(TextEncodingKind.Utf8Bom, LineEnding.CrLf);

        var result = await _service.WriteAsync(path, "x\r\n", format, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal([0xEF, 0xBB, 0xBF, (byte)'x', (byte)'\r', (byte)'\n'], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public async Task Recree_un_fichier_supprime_avec_son_dossier()
    {
        var path = Path.Combine(_folder, "supprime", "e.md");

        var result = await _service.WriteAsync(path, "y", new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal("y", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Donne_la_taille_d_un_fichier()
    {
        var path = Write("f.md", [1, 2, 3]);

        Assert.Equal(3, _service.GetSize(path).Data);
    }

    [Fact]
    public async Task Cree_un_fichier_markdown_vide_en_utf8_crlf()
    {
        var path = Path.Combine(_folder, "nouveau.md");

        var result = await _service.CreateAsync(path, TestContext.Current.CancellationToken);

        Assert.True(result.Ok);
        Assert.Equal(new TextFormat(TextEncodingKind.Utf8, LineEnding.CrLf), result.Data!.Format);
        Assert.Equal(0, new FileInfo(path).Length);
    }

    [Fact]
    public async Task Refuse_de_creer_un_fichier_existant_sans_le_modifier()
    {
        var path = Write("existant.md", Encoding.UTF8.GetBytes("contenu"));

        var result = await _service.CreateAsync(path, TestContext.Current.CancellationToken);

        Assert.False(result.Ok);
        Assert.Equal(FeedbackType.Danger, result.Error!.Type);
        Assert.Equal("contenu", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Refuse_de_creer_un_fichier_non_markdown()
    {
        var path = Path.Combine(_folder, "notes.txt");

        var result = await _service.CreateAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(FeedbackType.Warning, result.Error!.Type);
        Assert.False(File.Exists(path));
    }

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
