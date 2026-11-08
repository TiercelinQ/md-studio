using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdStudio.Helpers;
using MdStudio.Models;
using MdStudio.Services;
using Microsoft.Extensions.Logging;

namespace MdStudio.ViewModels;

/// <summary>Explorer pane (zone D): opened folder, filtered tree, file count.</summary>
public sealed partial class ExplorerViewModel : ObservableObject, IDisposable
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Dossier ouvert supprime {Root}")]
    private static partial void LogOpenedFolderDeleted(ILogger logger, string root);

    private readonly IFolderScanService _scanner;
    private readonly IFileWatchService _watcher;
    private readonly IFeedbackService _feedback;
    private readonly ILogger<ExplorerViewModel> _logger;
    private CancellationTokenSource? _refresh;
    private string? _selectedPath;

    /// <summary>Initializes a new instance of the <see cref="ExplorerViewModel"/> class.</summary>
    /// <param name="scanner">Folder scanner.</param>
    /// <param name="watcher">Disk watcher.</param>
    /// <param name="feedback">Notifications.</param>
    /// <param name="logger">Logger.</param>
    public ExplorerViewModel(IFolderScanService scanner, IFileWatchService watcher, IFeedbackService feedback, ILogger<ExplorerViewModel> logger)
    {
        _scanner = scanner;
        _watcher = watcher;
        _feedback = feedback;
        _logger = logger;
        _watcher.FolderChanged += OnFolderChanged;
    }

    /// <summary>Raised when the user opens a file of the tree.</summary>
    public event EventHandler<string>? FileOpenRequested;

    /// <summary>Gets the first-level rows of the tree.</summary>
    public ObservableCollection<FileNodeViewModel> Nodes { get; } = [];

    /// <summary>Gets the opened folder, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolder), nameof(HasNoFolder), nameof(IsEmptyFolder), nameof(HasFiles))]
    public partial string? RootPath { get; private set; }

    /// <summary>Gets the name of the opened folder.</summary>
    [ObservableProperty]
    public partial string RootName { get; private set; } = string.Empty;

    /// <summary>Gets the number of Markdown files of the opened folder, whatever the filter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FooterText), nameof(IsEmptyFolder), nameof(HasFiles))]
    public partial int FileCount { get; private set; }

    /// <summary>Gets or sets the filter text.</summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether the filter matches no file.</summary>
    [ObservableProperty]
    public partial bool HasNoMatch { get; private set; }

    /// <summary>Gets a value indicating whether a folder is opened.</summary>
    public bool HasFolder => RootPath is not null;

    /// <summary>Gets a value indicating whether no folder is opened.</summary>
    public bool HasNoFolder => RootPath is null;

    /// <summary>Gets a value indicating whether the opened folder holds no Markdown file.</summary>
    public bool IsEmptyFolder => HasFolder && FileCount == 0;

    /// <summary>Gets a value indicating whether the opened folder holds Markdown files.</summary>
    public bool HasFiles => HasFolder && FileCount > 0;

    /// <summary>Gets the footer text ("N fichiers Markdown").</summary>
    public string FooterText => FrenchPlural.MarkdownFiles(FileCount);

    /// <summary>Opens a folder, replacing the previous one.</summary>
    /// <param name="root">Absolute folder path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the folder was read.</returns>
    public async Task<bool> LoadAsync(string root, CancellationToken ct)
    {
        var result = await _scanner.ScanAsync(root, ct);
        if (!result.Ok)
        {
            _feedback.Show(result.Error!);
            return false;
        }

        var tree = result.Data!;
        RootPath = tree.FullPath;
        FilterText = string.Empty;
        Build(tree, tree.Children.Where(c => c.IsFolder).Select(c => c.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase));
        _watcher.WatchFolder(tree.FullPath);
        return true;
    }

    /// <summary>Highlights the row of the active document.</summary>
    /// <param name="path">Absolute path of the active document, or null.</param>
    public void SelectPath(string? path)
    {
        _selectedPath = path;
        foreach (var node in Nodes.SelectMany(n => n.SelfAndDescendants()))
        {
            node.IsSelected = !node.IsFolder && path is not null && string.Equals(node.FullPath, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _watcher.FolderChanged -= OnFolderChanged;
        _refresh?.Cancel();
        _refresh?.Dispose();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void Activate(FileNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (node.IsFolder)
        {
            node.IsOpen = !node.IsOpen;
        }
        else
        {
            FileOpenRequested?.Invoke(this, node.FullPath);
        }
    }

    private void Build(FileNode tree, IReadOnlySet<string> expanded)
    {
        Nodes.Clear();
        foreach (var child in tree.Children)
        {
            Nodes.Add(new FileNodeViewModel(child, 0, expanded));
        }

        RootName = tree.Name;
        FileCount = tree.FileCount;
        ApplyFilter();
        SelectPath(_selectedPath);
    }

    private void ApplyFilter()
    {
        var text = FilterText.Trim();
        var filter = text.Length == 0 ? null : text;
        var any = false;
        foreach (var node in Nodes)
        {
            any |= node.ApplyFilter(filter);
        }

        HasNoMatch = filter is not null && !any && FileCount > 0;
    }

    private void OnFolderChanged(object? sender, EventArgs e)
    {
        // Background source: FileSystemWatcher timer thread.
        Application.Current?.Dispatcher.InvokeAsync(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        if (RootPath is not { } root)
        {
            return;
        }

        _refresh?.Cancel();
        _refresh?.Dispose();
        _refresh = new CancellationTokenSource();
        var expanded = Nodes.SelectMany(n => n.SelfAndDescendants()).Where(n => n.IsExpanded).Select(n => n.FullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var result = await _scanner.ScanAsync(root, _refresh.Token);
            if (result.Ok && string.Equals(RootPath, root, StringComparison.OrdinalIgnoreCase))
            {
                Build(result.Data!, expanded);
            }
            else if (!result.Ok && !Directory.Exists(root))
            {
                LogOpenedFolderDeleted(_logger, root);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer refresh replaced this one.
        }
    }
}
