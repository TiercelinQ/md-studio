using System.Globalization;
using System.IO;
using MdStudio.Helpers;
using MdStudio.Models;
using MdStudio.Resources;
using Microsoft.Extensions.Logging;

namespace MdStudio.Services;

/// <summary>Reads and writes Markdown files while keeping their encoding and line endings.</summary>
public interface ITextFileService
{
    /// <summary>Returns the size of a file in bytes.</summary>
    /// <param name="path">Absolute path.</param>
    /// <returns>The size, or a failure when the file is missing or not accessible.</returns>
    Result<long> GetSize(string path);

    /// <summary>Reads and decodes a text file.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decoded file, or a failure (missing, denied, not text).</returns>
    Task<Result<TextFile>> ReadAsync(string path, CancellationToken ct);

    /// <summary>Writes a text file atomically in the given format. A deleted file is recreated.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="text">Content to write, line endings already set.</param>
    /// <param name="format">Encoding to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The written file, or a failure when the write did not happen.</returns>
    Task<Result<TextFile>> WriteAsync(string path, string text, TextFormat format, CancellationToken ct);

    /// <summary>Creates an empty Markdown file (UTF-8 without BOM, CRLF). Never overwrites an existing file.</summary>
    /// <param name="path">Absolute path with a Markdown extension.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created file, or a failure (invalid path, file already present, write denied).</returns>
    Task<Result<TextFile>> CreateAsync(string path, CancellationToken ct);
}

/// <summary>Default <see cref="ITextFileService"/> working on the local file system.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class TextFileService(ILogger<TextFileService> logger) : ITextFileService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Fichier introuvable {Path}")]
    private static partial void LogFileNotFound(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Acces refuse a {Path}")]
    private static partial void LogAccessDenied(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fichier non texte {Path}")]
    private static partial void LogNotText(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lecture refusee {Path}")]
    private static partial void LogReadDenied(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec d'ecriture {Path}")]
    private static partial void LogWriteFailed(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fichier temporaire non supprime {Path}")]
    private static partial void LogTempNotDeleted(ILogger logger, Exception? exception, string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Creation refusee, chemin non Markdown {Path}")]
    private static partial void LogCreateRejected(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de creation {Path}")]
    private static partial void LogCreateFailed(ILogger logger, Exception? exception, string path);

    /// <inheritdoc/>
    public Result<long> GetSize(string path)
    {
        try
        {
            return Result<long>.Success(new FileInfo(path).Length);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            LogFileNotFound(logger, ex, path);
            return Result<long>.Failure(NotFound(path));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogAccessDenied(logger, ex, path);
            return Result<long>.Failure(Denied(path));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TextFile>> ReadAsync(string path, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            if (!TextFormatDetector.TryDecode(bytes, out var text, out var format))
            {
                throw new NotTextFileException(path);
            }

            var info = new FileInfo(path);
            return Result<TextFile>.Success(new TextFile(path, text, format, info.LastWriteTimeUtc, info.Length));
        }
        catch (NotTextFileException ex)
        {
            LogNotText(logger, ex, path);
            return Result<TextFile>.Failure(new ResultError(FeedbackType.Danger, Format(Strings.Error_NotText, path)));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            LogFileNotFound(logger, ex, path);
            return Result<TextFile>.Failure(NotFound(path));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogReadDenied(logger, ex, path);
            return Result<TextFile>.Failure(Denied(path));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TextFile>> WriteAsync(string path, string text, TextFormat format, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(path) ?? throw new ArgumentException(path, nameof(path));
        var temp = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(temp, TextFormatDetector.Encode(text, format.Encoding), ct).ConfigureAwait(false);
            File.Move(temp, path, overwrite: true);
            var info = new FileInfo(path);
            return Result<TextFile>.Success(new TextFile(path, text, format, info.LastWriteTimeUtc, info.Length));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogWriteFailed(logger, ex, path);
            DeleteQuietly(temp);
            return Result<TextFile>.Failure(new ResultError(FeedbackType.Danger, Format(Strings.Error_SaveFailed, path)));
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(temp);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TextFile>> CreateAsync(string path, CancellationToken ct)
    {
        if (!Path.IsPathFullyQualified(path)
            || !AppConfig.MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            LogCreateRejected(logger, path);
            return Result<TextFile>.Failure(new ResultError(FeedbackType.Warning, Format(Strings.Error_NotMarkdown, path)));
        }

        ct.ThrowIfCancellationRequested();
        try
        {
            // CreateNew fails when the file appeared in the meantime: an existing file is never emptied.
            var stream = new FileStream(path, new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous });
            await stream.DisposeAsync().ConfigureAwait(false);
            var info = new FileInfo(path);
            return Result<TextFile>.Success(new TextFile(path, string.Empty, new TextFormat(TextEncodingKind.Utf8, LineEnding.CrLf), info.LastWriteTimeUtc, info.Length));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogCreateFailed(logger, ex, path);
            return Result<TextFile>.Failure(new ResultError(FeedbackType.Danger, Format(Strings.Error_CreateFailed, path)));
        }
    }

    private static ResultError NotFound(string path) => new(FeedbackType.Danger, Format(Strings.Error_NotFound, path));

    private static ResultError Denied(string path) => new(FeedbackType.Danger, Format(Strings.Error_OpenDenied, path));

    private static string Format(string template, string path) =>
        string.Format(CultureInfo.CurrentCulture, template, Path.GetFileName(path));

    private void DeleteQuietly(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            LogTempNotDeleted(logger, ex, temp);
        }
    }
}
