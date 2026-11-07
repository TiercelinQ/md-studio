namespace MdReader.Models;

/// <summary>A folder or a Markdown file of the opened folder.</summary>
/// <param name="Name">File or folder name.</param>
/// <param name="FullPath">Absolute path.</param>
/// <param name="IsFolder">True for a folder.</param>
/// <param name="Children">Sorted children (folders first), empty for a file.</param>
public sealed record FileNode(string Name, string FullPath, bool IsFolder, IReadOnlyList<FileNode> Children)
{
    /// <summary>Gets the number of Markdown files in this node and its descendants.</summary>
    public int FileCount => IsFolder ? Children.Sum(c => c.FileCount) : 1;
}
