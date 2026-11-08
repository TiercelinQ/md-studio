using System.Text;
using MdStudio.Helpers;
using MdStudio.Models;

namespace MdStudio.Services;

/// <summary>Rebuilds the Markdown text of an edited document, block by block (specification T-04).</summary>
public interface IMarkdownComposeService
{
    /// <summary>
    /// Composes the text: untouched blocks are copied from the source with their original separators,
    /// modified or new blocks are written with the file line ending.
    /// </summary>
    /// <param name="source">Source text the document was rendered from.</param>
    /// <param name="rendered">Rendering of <paramref name="source"/>.</param>
    /// <param name="blocks">Blocks of the edited page, in page order.</param>
    /// <param name="lineEnding">Line ending of the file.</param>
    /// <returns>The composed Markdown text.</returns>
    string Compose(string source, RenderedDocument rendered, IReadOnlyList<EditedBlock> blocks, LineEnding lineEnding);
}

/// <summary>Default <see cref="IMarkdownComposeService"/>.</summary>
public sealed class MarkdownComposeService : IMarkdownComposeService
{
    /// <inheritdoc/>
    public string Compose(string source, RenderedDocument rendered, IReadOnlyList<EditedBlock> blocks, LineEnding lineEnding)
    {
        var newLine = LineEndings.ToNewLine(lineEnding);
        var parts = new List<(int? Source, string Text)>(blocks.Count);
        foreach (var block in blocks)
        {
            if (block.Source is int index && index >= 0 && index < rendered.Blocks.Count)
            {
                var original = rendered.Blocks[index];
                parts.Add((index, source.Substring(original.Start, original.Length)));
            }
            else if (block.Markdown is { } markdown)
            {
                var text = LineEndings.Normalize(markdown, lineEnding).Trim('\r', '\n');
                if (text.Trim().Length > 0)
                {
                    parts.Add((null, text));
                }
            }
        }

        if (parts.Count == 0)
        {
            return rendered.Blocks.Count == 0 ? source : string.Empty;
        }

        var lastSource = rendered.Blocks.Count - 1;
        var trailing = rendered.Blocks.Count > 0 ? rendered.Blocks[lastSource].Separator : newLine;
        var builder = new StringBuilder(source.Length + 64).Append(rendered.Leading);
        for (var k = 0; k < parts.Count; k++)
        {
            var (index, text) = parts[k];
            builder.Append(text);
            if (k == parts.Count - 1)
            {
                builder.Append(index == lastSource ? rendered.Blocks[lastSource].Separator : trailing);
            }
            else if (index is int current && parts[k + 1].Source == current + 1)
            {
                builder.Append(rendered.Blocks[current].Separator);
            }
            else
            {
                builder.Append(newLine).Append(newLine);
            }
        }

        return builder.ToString();
    }
}
