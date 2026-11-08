using System.IO;
using MdStudio.Models;
using Microsoft.Extensions.Logging;

namespace MdStudio.Services;

/// <summary>Watches the opened folder and the opened files for changes made outside the application.</summary>
public interface IFileWatchService : IDisposable
{
    /// <summary>Raised on a background thread when Markdown files or folders changed under the watched folder.</summary>
    event EventHandler? FolderChanged;

    /// <summary>Raised on a background thread when a watched file changed or was deleted.</summary>
    event EventHandler<FileChange>? FileChanged;

    /// <summary>Watches a folder and its subfolders, replacing the previous one.</summary>
    /// <param name="root">Absolute folder path, or null to stop.</param>
    void WatchFolder(string? root);

    /// <summary>Starts watching a file.</summary>
    /// <param name="path">Absolute file path.</param>
    void WatchFile(string path);

    /// <summary>Stops watching a file.</summary>
    /// <param name="path">Absolute file path.</param>
    void UnwatchFile(string path);
}

/// <summary><see cref="FileSystemWatcher"/> implementation of <see cref="IFileWatchService"/>, events grouped over 300 ms.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class FileWatchService(ILogger<FileWatchService> logger) : IFileWatchService
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Surveillance du dossier interrompue, nouvelle lecture")]
    private static partial void LogWatcherError(ILogger logger, Exception? exception);

    private const int GroupDelayMs = 300;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, FileWatch> _files = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _folder;
    private Timer? _folderTimer;

    /// <inheritdoc/>
    public event EventHandler? FolderChanged;

    /// <inheritdoc/>
    public event EventHandler<FileChange>? FileChanged;

    /// <inheritdoc/>
    public void WatchFolder(string? root)
    {
        lock (_lock)
        {
            _folder?.Dispose();
            _folder = null;
            if (root is null || !Directory.Exists(root))
            {
                return;
            }

            _folderTimer ??= new Timer(_ => FolderChanged?.Invoke(this, EventArgs.Empty));
            _folder = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
            };
            _folder.Created += OnFolderEvent;
            _folder.Deleted += OnFolderEvent;
            _folder.Renamed += OnFolderEvent;
            _folder.Error += OnError;
            _folder.EnableRaisingEvents = true;
        }
    }

    /// <inheritdoc/>
    public void WatchFile(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        lock (_lock)
        {
            if (_files.ContainsKey(path))
            {
                return;
            }

            var watch = new FileWatch(path, this);
            _files[path] = watch;
        }
    }

    /// <inheritdoc/>
    public void UnwatchFile(string path)
    {
        lock (_lock)
        {
            if (_files.Remove(path, out var watch))
            {
                watch.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            _folder?.Dispose();
            _folderTimer?.Dispose();
            foreach (var watch in _files.Values)
            {
                watch.Dispose();
            }

            _files.Clear();
        }
    }

    private static bool IsRelevant(string path) =>
        !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)
        && (Directory.Exists(path)
            || string.IsNullOrEmpty(Path.GetExtension(path))
            || AppConfig.MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase));

    private void OnFolderEvent(object sender, FileSystemEventArgs e)
    {
        var renamedFrom = (e as RenamedEventArgs)?.OldFullPath;
        if (IsRelevant(e.FullPath) || (renamedFrom is not null && IsRelevant(renamedFrom)))
        {
            _folderTimer?.Change(GroupDelayMs, Timeout.Infinite);
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        LogWatcherError(logger, e.GetException());
        _folderTimer?.Change(GroupDelayMs, Timeout.Infinite);
    }

    private void Raise(FileChange change) => FileChanged?.Invoke(this, change);

    private sealed class FileWatch : IDisposable
    {
        private readonly string _path;
        private readonly FileWatchService _owner;
        private readonly FileSystemWatcher _watcher;
        private readonly Timer _timer;
        private FileChangeKind _kind;

        public FileWatch(string path, FileWatchService owner)
        {
            _path = path;
            _owner = owner;
            _timer = new Timer(_ => _owner.Raise(new FileChange(_path, _kind)));
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            _watcher.Changed += (_, _) => Schedule(FileChangeKind.Changed);
            _watcher.Created += (_, _) => Schedule(FileChangeKind.Changed);
            _watcher.Deleted += (_, _) => Schedule(FileChangeKind.Deleted);
            _watcher.Renamed += (_, e) => Schedule(
                string.Equals(e.FullPath, _path, StringComparison.OrdinalIgnoreCase) ? FileChangeKind.Changed : FileChangeKind.Deleted);
            _watcher.EnableRaisingEvents = true;
        }

        public void Dispose()
        {
            _watcher.Dispose();
            _timer.Dispose();
        }

        private void Schedule(FileChangeKind kind)
        {
            _kind = kind;
            _timer.Change(GroupDelayMs, Timeout.Infinite);
        }
    }
}
