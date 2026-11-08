using System.Text;
using MdStudio.Helpers;
using MdStudio.Models;

namespace MdStudio.Tests.Helpers;

public sealed class TextFormatDetectorTests
{
    [Fact]
    public void Decode_un_utf8_sans_bom()
    {
        Assert.True(TextFormatDetector.TryDecode(Encoding.UTF8.GetBytes("é\nb"), out var text, out var format));
        Assert.Equal("é\nb", text);
        Assert.Equal(new TextFormat(TextEncodingKind.Utf8, LineEnding.Lf), format);
    }

    [Fact]
    public void Decode_un_utf16_big_endian() =>
        Assert.True(TextFormatDetector.TryDecode([0xFE, 0xFF, 0x00, 0x41], out var text, out _) && text == "A");

    [Fact]
    public void Refuse_un_utf8_invalide() => Assert.False(TextFormatDetector.TryDecode([0xC3, 0x28], out _, out _));

    [Fact]
    public void Refuse_un_caractere_nul() => Assert.False(TextFormatDetector.TryDecode([0x41, 0x00], out _, out _));

    [Theory]
    [InlineData("a\r\nb\r\nc\n", LineEnding.CrLf)]
    [InlineData("a\nb\nc\r\n", LineEnding.Lf)]
    [InlineData("sans saut", LineEnding.CrLf)]
    public void Retient_la_fin_de_ligne_majoritaire(string text, LineEnding expected) =>
        Assert.Equal(expected, TextFormatDetector.DetectLineEnding(text));

    [Fact]
    public void Encode_avec_bom_sauf_en_utf8_simple()
    {
        Assert.Equal([0x41], TextFormatDetector.Encode("A", TextEncodingKind.Utf8));
        Assert.Equal([0xEF, 0xBB, 0xBF, 0x41], TextFormatDetector.Encode("A", TextEncodingKind.Utf8Bom));
        Assert.Equal([0xFF, 0xFE, 0x41, 0x00], TextFormatDetector.Encode("A", TextEncodingKind.Utf16Le));
        Assert.Equal([0xFE, 0xFF, 0x00, 0x41], TextFormatDetector.Encode("A", TextEncodingKind.Utf16Be));
    }
}
