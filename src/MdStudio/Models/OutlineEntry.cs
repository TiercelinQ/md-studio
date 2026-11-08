namespace MdStudio.Models;

/// <summary>A heading of the document outline.</summary>
/// <param name="Level">Heading level, 1 to 3.</param>
/// <param name="Text">Heading text.</param>
/// <param name="Anchor">Identifier of the heading in the page.</param>
public sealed record OutlineEntry(int Level, string Text, string Anchor);
