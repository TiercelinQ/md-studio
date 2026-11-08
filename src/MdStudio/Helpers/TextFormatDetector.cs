using System.Diagnostics.CodeAnalysis;
using System.Text;
using MdStudio.Models;

namespace MdStudio.Helpers;

/// <summary>Detects and applies the encoding and line ending of a text file.</summary>
public static class TextFormatDetector
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Decodes bytes: UTF-8 or UTF-16 with BOM, or strict UTF-8 without BOM. A NUL character means binary.</summary>
    /// <param name="bytes">File content.</param>
    /// <param name="text">Decoded text.</param>
    /// <param name="format">Detected format.</param>
    /// <returns>False when the content is binary or not valid text.</returns>
    public static bool TryDecode(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out string? text, [NotNullWhen(true)] out TextFormat? format)
    {
        text = null;
        format = null;
        TextEncodingKind kind;
        Encoding encoding;
        int skip;
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            (kind, encoding, skip) = (TextEncodingKind.Utf8Bom, StrictUtf8, 3);
        }
        else if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            (kind, encoding, skip) = (TextEncodingKind.Utf16Le, new UnicodeEncoding(false, false, true), 2);
        }
        else if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            (kind, encoding, skip) = (TextEncodingKind.Utf16Be, new UnicodeEncoding(true, false, true), 2);
        }
        else
        {
            (kind, encoding, skip) = (TextEncodingKind.Utf8, StrictUtf8, 0);
        }

        try
        {
            var decoded = encoding.GetString(bytes[skip..]);
            if (decoded.Contains('\0', StringComparison.Ordinal))
            {
                return false;
            }

            text = decoded;
            format = new TextFormat(kind, DetectLineEnding(decoded));
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>Returns the dominant line ending; CRLF when the text has no line break.</summary>
    /// <param name="text">Decoded text.</param>
    /// <returns>The line ending.</returns>
    public static LineEnding DetectLineEnding(string text)
    {
        var crlf = 0;
        var lf = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\r')
                {
                    crlf++;
                }
                else
                {
                    lf++;
                }
            }
        }

        return lf > crlf ? LineEnding.Lf : LineEnding.CrLf;
    }

    /// <summary>Encodes text, with a BOM for every kind except plain UTF-8.</summary>
    /// <param name="text">Text to write.</param>
    /// <param name="kind">Target encoding.</param>
    /// <returns>The bytes.</returns>
    public static byte[] Encode(string text, TextEncodingKind kind)
    {
        Encoding encoding = kind switch
        {
            TextEncodingKind.Utf8Bom => new UTF8Encoding(true),
            TextEncodingKind.Utf16Le => new UnicodeEncoding(false, true),
            TextEncodingKind.Utf16Be => new UnicodeEncoding(true, true),
            _ => new UTF8Encoding(false),
        };
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}
