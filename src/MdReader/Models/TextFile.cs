namespace MdReader.Models;

/// <summary>A text file read from or written to disk.</summary>
/// <param name="Path">Absolute path.</param>
/// <param name="Text">Decoded content, original line endings kept.</param>
/// <param name="Format">Encoding and line ending.</param>
/// <param name="LastWriteUtc">Last write time on disk, used to ignore the application's own writes.</param>
/// <param name="Length">Size in bytes on disk.</param>
public sealed record TextFile(string Path, string Text, TextFormat Format, DateTime LastWriteUtc, long Length);
