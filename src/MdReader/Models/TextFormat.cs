namespace MdReader.Models;

/// <summary>Text encoding detected in a file and reused when writing it back.</summary>
public enum TextEncodingKind
{
    /// <summary>UTF-8 without byte order mark.</summary>
    Utf8,

    /// <summary>UTF-8 with byte order mark.</summary>
    Utf8Bom,

    /// <summary>UTF-16 little endian with byte order mark.</summary>
    Utf16Le,

    /// <summary>UTF-16 big endian with byte order mark.</summary>
    Utf16Be,
}

/// <summary>Line ending detected in a file and reused for new content.</summary>
public enum LineEnding
{
    /// <summary>Carriage return + line feed (Windows).</summary>
    CrLf,

    /// <summary>Line feed only.</summary>
    Lf,
}

/// <summary>On-disk format of a text file.</summary>
/// <param name="Encoding">Text encoding.</param>
/// <param name="LineEnding">Dominant line ending.</param>
public sealed record TextFormat(TextEncodingKind Encoding, LineEnding LineEnding);
