using System.Globalization;
using System.IO;
using System.Text;
using MdStudio.Helpers;
using MdStudio.Models;
using MdStudio.Resources;
using Microsoft.Extensions.Logging;

namespace MdStudio.Services;

/// <summary>Builds the Markdown tree of a folder.</summary>
public interface IFolderScanService
{
    /// <summary>
    /// Scans a folder recursively off the UI thread: Markdown files only, folders without Markdown
    /// hidden, folders first, natural order.
    /// </summary>
    /// <param name="root">Absolute path of the folder.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The root node, or a failure when the folder cannot be read.</returns>
    Task<Result<FileNode>> ScanAsync(string root, CancellationToken ct);
}

/// <summary>File system implementation of <see cref="IFolderScanService"/>.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class FolderScanService(ILogger<FolderScanService> logger) : IFolderScanService
{
    private static readonly CompositeFormat FolderUnreadableFormat = CompositeFormat.Parse(Strings.Toast_FolderUnreadable_Message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dossier illisible {Root}")]
    private static partial void LogFolderUnreadable(ILogger logger, Exception? exception, string root);

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
    };

    /// <inheritdoc/>
    public async Task<Result<FileNode>> ScanAsync(string root, CancellationToken ct)
    {
        try
        {
            var directory = new DirectoryInfo(Path.GetFullPath(root));
            if (!directory.Exists)
            {
                throw new DirectoryNotFoundException(root);
            }

            var node = await Task.Run(() => Scan(directory, ct), ct).ConfigureAwait(false);
            return Result<FileNode>.Success(node);
        }
        catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or IOException)
        {
            LogFolderUnreadable(logger, ex, root);
            return Result<FileNode>.Failure(new ResultError(
                FeedbackType.Danger,
                Strings.Toast_FolderUnreadable_Title,
                string.Format(CultureInfo.CurrentCulture, FolderUnreadableFormat, Path.GetFileName(root))));
        }
    }

    private static FileNode Scan(DirectoryInfo directory, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var folders = directory.EnumerateDirectories("*", Options)
            .Select(d => Scan(d, ct))
            .Where(n => n.FileCount > 0)
            .OrderBy(n => n.Name, NaturalStringComparer.Instance);
        var files = directory.EnumerateFiles("*", Options)
            .Where(f => AppConfig.MarkdownExtensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase))
            .Select(f => new FileNode(f.Name, f.FullName, false, []))
            .OrderBy(n => n.Name, NaturalStringComparer.Instance);
        return new FileNode(directory.Name, directory.FullName, true, [.. folders, .. files]);
    }
}
