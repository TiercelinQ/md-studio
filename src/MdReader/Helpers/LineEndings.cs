using MdReader.Models;

namespace MdReader.Helpers;

/// <summary>Line ending conversions.</summary>
public static class LineEndings
{
    /// <summary>Returns the characters of a line ending.</summary>
    /// <param name="ending">Line ending.</param>
    /// <returns><c>\r\n</c> or <c>\n</c>.</returns>
    public static string ToNewLine(LineEnding ending) => ending == LineEnding.CrLf ? "\r\n" : "\n";

    /// <summary>Rewrites every line break of a text with one line ending.</summary>
    /// <param name="text">Text with any line breaks.</param>
    /// <param name="ending">Target line ending.</param>
    /// <returns>The normalized text.</returns>
    public static string Normalize(string text, LineEnding ending) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", ToNewLine(ending), StringComparison.Ordinal);
}
