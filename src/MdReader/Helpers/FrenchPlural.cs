using System.Globalization;
using MdReader.Resources;

namespace MdReader.Helpers;

/// <summary>Counters with French singular and plural (specification C15).</summary>
public static class FrenchPlural
{
    /// <summary>Formats a word count: "1 mot", "N mots".</summary>
    /// <param name="count">Number of words.</param>
    /// <returns>The label.</returns>
    public static string Words(int count) => Format(count, Strings.Words_One, Strings.Words_Many);

    /// <summary>Formats a file count: "1 fichier Markdown", "N fichiers Markdown".</summary>
    /// <param name="count">Number of files.</param>
    /// <returns>The label.</returns>
    public static string MarkdownFiles(int count) => Format(count, Strings.Files_One, Strings.Files_Many);

    private static string Format(int count, string one, string many) =>
        count is 0 or 1 ? string.Format(CultureInfo.CurrentCulture, one, count) : string.Format(CultureInfo.CurrentCulture, many, count);
}
