namespace MdReader.Models;

/// <summary>A block of the edited page: either an untouched source block or new Markdown.</summary>
/// <param name="Source">Index of the untouched source block, or null.</param>
/// <param name="Markdown">Markdown of a modified or new block, or null.</param>
public sealed record EditedBlock(int? Source, string? Markdown);
