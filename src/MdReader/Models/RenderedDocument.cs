namespace MdReader.Models;

/// <summary>A top-level Markdown block and its position in the source text.</summary>
/// <param name="Index">Position of the block in the document, also written as <c>data-src</c> in the page.</param>
/// <param name="Start">Offset of the first character of the block.</param>
/// <param name="Length">Number of characters of the block.</param>
/// <param name="Separator">Source text between this block and the next one (or the end of the text).</param>
public sealed record SourceBlock(int Index, int Start, int Length, string Separator);

/// <summary>A Markdown text rendered to HTML, block by block.</summary>
/// <param name="Html">Sanitized HTML, one wrapper element per source block.</param>
/// <param name="Leading">Source text before the first block.</param>
/// <param name="Blocks">Source blocks in document order.</param>
public sealed record RenderedDocument(string Html, string Leading, IReadOnlyList<SourceBlock> Blocks);
