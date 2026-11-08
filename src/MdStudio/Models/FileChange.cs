namespace MdStudio.Models;

/// <summary>Kind of change observed on disk.</summary>
public enum FileChangeKind
{
    /// <summary>The file was created.</summary>
    Created,

    /// <summary>The file content changed.</summary>
    Changed,

    /// <summary>The file was deleted.</summary>
    Deleted,

    /// <summary>The file was renamed.</summary>
    Renamed,
}

/// <summary>A change observed on an opened file.</summary>
/// <param name="Path">Absolute path of the watched file.</param>
/// <param name="Kind">Kind of change.</param>
public sealed record FileChange(string Path, FileChangeKind Kind);
