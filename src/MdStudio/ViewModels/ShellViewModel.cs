using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdStudio.Models;
using MdStudio.Resources;
using MdStudio.Services;
using Microsoft.Extensions.Logging;

namespace MdStudio.ViewModels;

/// <summary>Main window: tabs, mode, panes, zoom, theme, creating, opening, saving and closing.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Chemin ignore a l'ouverture {Path}")]
    private static partial void LogPathIgnored(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la page du document")]
    private static partial void LogPageFailure(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fichier {Name} lu et rendu en {ElapsedMs} ms")]
    private static partial void LogFilePrepared(ILogger logger, string name, double elapsedMs);

    private static readonly CompositeFormat FileExistsFormat = CompositeFormat.Parse(Strings.Toast_FileExists_Message);

    private static readonly string[] FormatProperties =
    [
        nameof(SelectedBlockStyle), nameof(BoldToggle), nameof(ItalicToggle), nameof(StrikeToggle), nameof(BulletListToggle), nameof(NumberedListToggle),
    ];

    private readonly Func<DocumentViewModel> _documentFactory;
    private readonly Func<LinkDialogViewModel> _linkFactory;
    private readonly ITextFileService _files;
    private readonly IMarkdownRenderService _renderer;
    private readonly IDialogService _dialogs;
    private readonly IFeedbackService _feedback;
    private readonly IPreferencesService _preferences;
    private readonly IAppThemeService _theme;
    private readonly IFileWatchService _watcher;
    private readonly IPageAssetProvider _assets;
    private readonly IAppPaths _paths;
    private readonly ILogger<ShellViewModel> _logger;

    /// <summary>Initializes a new instance of the <see cref="ShellViewModel"/> class.</summary>
    /// <param name="explorer">Explorer pane.</param>
    /// <param name="documentFactory">Creates a document.</param>
    /// <param name="linkFactory">Creates the content of the link dialog.</param>
    /// <param name="files">File access.</param>
    /// <param name="renderer">Markdown rendering.</param>
    /// <param name="dialogs">Pickers and dialogs.</param>
    /// <param name="feedback">Notifications.</param>
    /// <param name="preferences">Persisted settings.</param>
    /// <param name="theme">Theme.</param>
    /// <param name="watcher">Disk watcher.</param>
    /// <param name="assets">Page resources.</param>
    /// <param name="paths">Application paths.</param>
    /// <param name="logger">Logger.</param>
    public ShellViewModel(
        ExplorerViewModel explorer,
        Func<DocumentViewModel> documentFactory,
        Func<LinkDialogViewModel> linkFactory,
        ITextFileService files,
        IMarkdownRenderService renderer,
        IDialogService dialogs,
        IFeedbackService feedback,
        IPreferencesService preferences,
        IAppThemeService theme,
        IFileWatchService watcher,
        IPageAssetProvider assets,
        IAppPaths paths,
        ILogger<ShellViewModel> logger)
    {
        Explorer = explorer;
        _documentFactory = documentFactory;
        _linkFactory = linkFactory;
        _files = files;
        _renderer = renderer;
        _dialogs = dialogs;
        _feedback = feedback;
        _preferences = preferences;
        _theme = theme;
        _watcher = watcher;
        _assets = assets;
        _paths = paths;
        _logger = logger;

        Explorer.FileOpenRequested += OnOpenFileRequested;
        _theme.ThemeChanged += OnThemeChanged;
        _watcher.FileChanged += OnFileChanged;
    }

    /// <summary>Gets the explorer pane.</summary>
    public ExplorerViewModel Explorer { get; }

    /// <summary>Gets the opened documents, in opening order.</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>Gets the WebView2 user data folder.</summary>
    public string WebViewDataFolder => _paths.WebViewData;

    /// <summary>Gets the URL of the container page.</summary>
    public string HostPageUrl => _assets.HostPageUrl;

    /// <summary>Gets or sets the active document.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(HasNoDocument), nameof(IsFormatBarVisible), nameof(SelectedBlockStyle), nameof(BoldToggle), nameof(ItalicToggle), nameof(StrikeToggle), nameof(BulletListToggle), nameof(NumberedListToggle))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(ToggleModeCommand), nameof(SetModeCommand), nameof(OpenSearchCommand))]
    public partial DocumentViewModel? ActiveDocument { get; set; }

    /// <summary>Gets the window mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReading), nameof(IsEditing), nameof(IsSource), nameof(ModeText), nameof(IsFormatBarVisible))]
    public partial DocumentMode Mode { get; private set; }

    /// <summary>Gets a value indicating whether the explorer is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerDocked), nameof(IsExplorerOverlay), nameof(ExplorerToggle))]
    public partial bool IsExplorerVisible { get; private set; }

    /// <summary>Gets a value indicating whether the outline is shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOutlineDocked), nameof(IsOutlineOverlay), nameof(OutlineToggle))]
    public partial bool IsOutlineVisible { get; private set; }

    /// <summary>Gets the docked explorer width.</summary>
    [ObservableProperty]
    public partial double ExplorerWidth { get; private set; } = AppConfig.ExplorerWidthDefault;

    /// <summary>Gets a value indicating whether the window is at most 820 wide (overlay panes).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerDocked), nameof(IsExplorerOverlay), nameof(IsOutlineDocked), nameof(IsOutlineOverlay))]
    public partial bool IsNarrow { get; private set; }

    /// <summary>Gets a value indicating whether the window is at most 560 wide (wrapped command bar).</summary>
    [ObservableProperty]
    public partial bool IsCompact { get; private set; }

    /// <summary>Gets the zoom factor.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double Zoom { get; private set; } = 1.0;

    /// <summary>Gets a value indicating whether a document is open.</summary>
    public bool HasDocument => ActiveDocument is not null;

    /// <summary>Gets a value indicating whether no document is open (empty state).</summary>
    public bool HasNoDocument => ActiveDocument is null;

    /// <summary>Gets or sets a value indicating whether the mode is reading (segment binding).</summary>
    public bool IsReading
    {
        get => Mode == DocumentMode.Reading;
        set
        {
            if (value)
            {
                SetModeCommand.Execute(DocumentMode.Reading);
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the mode is editing (segment binding).</summary>
    public bool IsEditing
    {
        get => Mode == DocumentMode.Editing;
        set
        {
            if (value && HasDocument)
            {
                SetModeCommand.Execute(DocumentMode.Editing);
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the mode is source (segment binding).</summary>
    public bool IsSource
    {
        get => Mode == DocumentMode.Source;
        set
        {
            if (value && HasDocument)
            {
                SetModeCommand.Execute(DocumentMode.Source);
            }
        }
    }

    /// <summary>
    /// Gets or sets the explorer visibility from its toggle button. Setting it runs the toggle, so the
    /// UI Automation Toggle pattern (screen readers) behaves like a click.
    /// </summary>
    public bool ExplorerToggle
    {
        get => IsExplorerVisible;
        set
        {
            if (value != IsExplorerVisible)
            {
                ToggleExplorerCommand.Execute(null);
            }
        }
    }

    /// <summary>Gets or sets the outline visibility from its toggle button (see <see cref="ExplorerToggle"/>).</summary>
    public bool OutlineToggle
    {
        get => IsOutlineVisible;
        set
        {
            if (value != IsOutlineVisible)
            {
                ToggleOutlineCommand.Execute(null);
            }
        }
    }

    /// <summary>Gets or sets the Bold toggle of the formatting bar; setting it applies the command (Toggle pattern included).</summary>
    public bool BoldToggle
    {
        get => ActiveDocument?.FormatState.Bold ?? false;
        set => ToggleFormat(value, BoldToggle, FormatAction.Bold, nameof(BoldToggle));
    }

    /// <summary>Gets or sets the Italic toggle of the formatting bar; setting it applies the command (Toggle pattern included).</summary>
    public bool ItalicToggle
    {
        get => ActiveDocument?.FormatState.Italic ?? false;
        set => ToggleFormat(value, ItalicToggle, FormatAction.Italic, nameof(ItalicToggle));
    }

    /// <summary>Gets or sets the Strike toggle of the formatting bar; setting it applies the command (Toggle pattern included).</summary>
    public bool StrikeToggle
    {
        get => ActiveDocument?.FormatState.Strike ?? false;
        set => ToggleFormat(value, StrikeToggle, FormatAction.Strikethrough, nameof(StrikeToggle));
    }

    /// <summary>Gets or sets the BulletList toggle of the formatting bar; setting it applies the command (Toggle pattern included).</summary>
    public bool BulletListToggle
    {
        get => ActiveDocument?.FormatState.BulletList ?? false;
        set => ToggleFormat(value, BulletListToggle, FormatAction.BulletList, nameof(BulletListToggle));
    }

    /// <summary>Gets or sets the NumberedList toggle of the formatting bar; setting it applies the command (Toggle pattern included).</summary>
    public bool NumberedListToggle
    {
        get => ActiveDocument?.FormatState.NumberedList ?? false;
        set => ToggleFormat(value, NumberedListToggle, FormatAction.NumberedList, nameof(NumberedListToggle));
    }

    /// <summary>Gets a value indicating whether the formatting bar is shown.</summary>
    public bool IsFormatBarVisible => IsEditing && HasDocument;

    /// <summary>Gets a value indicating whether the explorer is docked.</summary>
    public bool IsExplorerDocked => IsExplorerVisible && !IsNarrow;

    /// <summary>Gets a value indicating whether the explorer is an overlay pane.</summary>
    public bool IsExplorerOverlay => IsExplorerVisible && IsNarrow;

    /// <summary>Gets a value indicating whether the outline is docked.</summary>
    public bool IsOutlineDocked => IsOutlineVisible && !IsNarrow;

    /// <summary>Gets a value indicating whether the outline is an overlay pane.</summary>
    public bool IsOutlineOverlay => IsOutlineVisible && IsNarrow;

    /// <summary>Gets the mode label of the status bar.</summary>
    public string ModeText => Mode switch
    {
        DocumentMode.Editing => Strings.Mode_Editing,
        DocumentMode.Source => Strings.Mode_Source,
        _ => Strings.Mode_Reading,
    };

    /// <summary>Gets the zoom label ("100 %", non-breaking space).</summary>
    public string ZoomText => $"{Math.Round(Zoom * 100)} %";

    /// <summary>Gets or sets the paragraph style of the formatting bar; setting it applies the style.</summary>
    public int SelectedBlockStyle
    {
        get => ActiveDocument?.BlockStyleIndex ?? 0;
        set
        {
            if (ActiveDocument?.Surface is { } surface && value != ActiveDocument.BlockStyleIndex && value >= 0)
            {
                surface.ApplyFormat(value switch
                {
                    1 => FormatAction.Heading1,
                    2 => FormatAction.Heading2,
                    3 => FormatAction.Heading3,
                    4 => FormatAction.Quote,
                    _ => FormatAction.Paragraph,
                });
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Restores the settings and opens the folder of the last session and the paths given at launch.</summary>
    /// <param name="width">Initial window width.</param>
    /// <param name="paths">Paths of the command line.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when done.</returns>
    public async Task InitializeAsync(double width, IReadOnlyList<string> paths, CancellationToken ct)
    {
        var settings = _preferences.Current;
        ExplorerWidth = settings.ExplorerWidth;
        Zoom = settings.Zoom;
        UpdateWidth(width);
        if (!IsNarrow)
        {
            IsExplorerVisible = settings.ExplorerVisible ?? true;
            IsOutlineVisible = settings.OutlineVisible ?? true;
        }

        if (settings.LastFolder is { } folder && Directory.Exists(folder))
        {
            await Explorer.LoadAsync(folder, ct);
        }

        await OpenPathsAsync(paths, ct);
    }

    /// <summary>Applies the width rules of the window (specification 3.6).</summary>
    /// <param name="width">Window width.</param>
    public void UpdateWidth(double width)
    {
        var narrow = width <= AppConfig.NarrowWidthThreshold;
        IsCompact = width <= AppConfig.CompactWidthThreshold;
        if (narrow == IsNarrow)
        {
            return;
        }

        IsNarrow = narrow;
        if (narrow)
        {
            IsExplorerVisible = false;
            IsOutlineVisible = false;
        }
        else
        {
            IsExplorerVisible = _preferences.Current.ExplorerVisible ?? true;
            IsOutlineVisible = _preferences.Current.OutlineVisible ?? true;
        }
    }

    /// <summary>Opens files and folders (drop, command line, second launch).</summary>
    /// <param name="paths">Absolute paths.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when all are opened.</returns>
    public async Task OpenPathsAsync(IReadOnlyList<string> paths, CancellationToken ct)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                await LoadFolderAsync(path, ct);
            }
            else if (File.Exists(path) && AppConfig.MarkdownExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                await OpenPathAsync(path, ct);
            }
            else
            {
                LogPathIgnored(_logger, path);
            }
        }
    }

    /// <summary>Opens a Markdown file in a tab, or activates its tab when already open.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when the tab is active.</returns>
    public async Task OpenPathAsync(string path, CancellationToken ct)
    {
        var full = Path.GetFullPath(path);
        var existing = Documents.FirstOrDefault(d => string.Equals(d.FilePath, full, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            ActiveDocument = existing;
            CloseExplorerOverlay();
            return;
        }

        var size = _files.GetSize(full);
        if (!size.Ok)
        {
            _feedback.Show(size.Error!);
            return;
        }

        if (size.Data > AppConfig.LargeFileThresholdBytes
            && !await _dialogs.ConfirmLargeFileAsync(Path.GetFileName(full), Math.Round(size.Data / 1048576.0, 1), ct))
        {
            return;
        }

        // The tab and its loading screen appear at once; a large file can take seconds to read and render.
        var document = _documentFactory();
        document.Prepare(full, new DocumentPageState(Mode, Zoom, _theme.PageTheme), Explorer.RootPath);
        document.OpenFileRequested += OnOpenFileRequested;
        document.LinkRequested += OnLinkRequested;
        document.OutlineNavigated += OnOutlineNavigated;
        Documents.Add(document);
        ActiveDocument = document;
        CloseExplorerOverlay();

        var started = Stopwatch.GetTimestamp();
        Result<TextFile> read;
        RenderedDocument? rendered = null;
        try
        {
            read = await _files.ReadAsync(full, ct);
            if (read.Ok)
            {
                rendered = await _renderer.RenderAsync(read.Data!, ct);
            }
        }
        catch (OperationCanceledException)
        {
            Remove(document);
            throw;
        }

        if (!read.Ok)
        {
            Remove(document);
            _feedback.Show(read.Error!);
            return;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var name = Path.GetFileName(full);
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            LogFilePrepared(_logger, name, elapsedMs);
        }

        // The tab may have been closed while the file was read.
        if (Documents.Contains(document))
        {
            document.Show(read.Data!, rendered!);
            _watcher.WatchFile(full);
        }
    }

    /// <summary>Closes a tab, asking to save a modified document first.</summary>
    /// <param name="document">Document to close.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when closed.</returns>
    public async Task<bool> TryCloseAsync(DocumentViewModel document, CancellationToken ct)
    {
        if (document.IsModified)
        {
            ActiveDocument = document;
            var decision = await _dialogs.ConfirmSaveAsync(document.FileName, ct);
            if (decision == SaveDecision.Cancel || (decision == SaveDecision.Save && !await document.SaveAsync(ct)))
            {
                return false;
            }
        }

        Remove(document);
        return true;
    }

    /// <summary>Asks to save every modified document before the window closes (F-ENR-08).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the window may close.</returns>
    public async Task<bool> RequestWindowCloseAsync(CancellationToken ct)
    {
        foreach (var document in Documents.Where(d => d.IsModified).ToList())
        {
            ActiveDocument = document;
            var decision = await _dialogs.ConfirmSaveAsync(document.FileName, ct);
            if (decision == SaveDecision.Cancel || (decision == SaveDecision.Save && !await document.SaveAsync(ct)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Saves the window position for the next launch.</summary>
    /// <param name="bounds">Restored bounds of the window.</param>
    /// <param name="maximized">True when maximized.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when persisted.</returns>
    public Task SaveWindowPlacementAsync(WindowBounds bounds, bool maximized, CancellationToken ct) =>
        _preferences.UpdateAsync(p => p with { WindowBounds = bounds, WindowMaximized = maximized }, ct);

    /// <summary>Changes the docked explorer width (separator drag or arrow keys) and persists it.</summary>
    /// <param name="width">New width, clamped to 180..420.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when persisted.</returns>
    public Task CommitExplorerWidthAsync(double width, CancellationToken ct)
    {
        ExplorerWidth = Math.Clamp(Math.Round(width), AppConfig.ExplorerWidthMin, AppConfig.ExplorerWidthMax);
        return _preferences.UpdateAsync(p => p with { ExplorerWidth = ExplorerWidth }, ct);
    }

    /// <summary>Routes a message of the page to its document.</summary>
    /// <param name="message">Message.</param>
    public void HandlePageMessage(PageMessage message)
    {
        var document = Documents.FirstOrDefault(d => d.Id == message.Tab);
        if (message.Type == "zoomWheel")
        {
            _ = ChangeZoomAsync((message.Delta ?? 0) > 0 ? AppConfig.ZoomStep : -AppConfig.ZoomStep);
        }
        else
        {
            document?.HandlePageMessage(message);
        }
    }

    /// <summary>Runs the command of a keyboard shortcut (specification 4.16).</summary>
    /// <param name="key">Key name: a letter, <c>Tab</c>, <c>Plus</c>, <c>Minus</c>, <c>0</c> or <c>Escape</c>.</param>
    /// <param name="control">True when Control is held.</param>
    /// <param name="shift">True when Shift is held.</param>
    /// <returns>True when the shortcut was handled.</returns>
    public bool HandleShortcut(string key, bool control, bool shift)
    {
        (IRelayCommand Command, object? Parameter)? shortcut = (key, control, shift) switch
        {
            ("N", true, false) => (CreateFileCommand, null),
            ("O", true, false) => (OpenFileCommand, null),
            ("O", true, true) => (OpenFolderCommand, null),
            ("S", true, false) => (SaveCommand, null),
            ("E", true, false) => (ToggleModeCommand, null),
            ("U", true, false) => (SetModeCommand, DocumentMode.Source),
            ("F", true, false) => (OpenSearchCommand, null),
            ("W", true, false) => (CloseTabCommand, null),
            ("Tab", true, false) => (NextTabCommand, null),
            ("Tab", true, true) => (PreviousTabCommand, null),
            ("Plus", true, _) => (ZoomInCommand, null),
            ("Minus", true, _) => (ZoomOutCommand, null),
            ("0", true, false) => (ZoomResetCommand, null),
            ("Escape", false, false) => (EscapeCommand, null),
            _ => null,
        };

        if (shortcut is not ({ } command, var parameter))
        {
            return false;
        }

        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }

        return true;
    }

    /// <summary>Changes the zoom by one step from the mouse wheel.</summary>
    /// <param name="delta">Wheel delta, positive to zoom in.</param>
    public void ZoomByWheel(int delta) => _ = ChangeZoomAsync(delta > 0 ? AppConfig.ZoomStep : -AppConfig.ZoomStep);

    /// <summary>Resolves a resource requested by the page.</summary>
    /// <param name="uri">Requested URI.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resource, or null.</returns>
    public Task<PageAsset?> ResolvePageResourceAsync(Uri uri, CancellationToken ct) => _assets.ResolveAsync(uri, ct);

    /// <summary>Logs a failure of the document page and tells the user.</summary>
    /// <param name="exception">The failure.</param>
    public void ReportPageFailure(Exception exception)
    {
        LogPageFailure(_logger, exception);
        _feedback.Show(new ResultError(FeedbackType.Danger, Strings.Toast_WebView_Title, Strings.Toast_WebView_Message));
    }

    /// <summary>Handles paths sent by a second launch: opens them and returns.</summary>
    /// <param name="paths">Absolute paths.</param>
    /// <returns>A task completed when opened.</returns>
    public Task ReceivePathsAsync(IReadOnlyList<string> paths) => OpenPathsAsync(paths, CancellationToken.None);

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnActiveDocumentPropertyChanged;
            oldValue.CloseSearch();
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnActiveDocumentPropertyChanged;
        }

        Explorer.SelectPath(newValue?.FilePath);
        if (newValue is null && !IsReading)
        {
            Mode = DocumentMode.Reading;
        }
    }

    private static bool CanSave(DocumentViewModel? document) => document?.IsModified == true;

    [RelayCommand]
    private async Task CreateFileAsync(CancellationToken ct)
    {
        if (_dialogs.PickNewMarkdownFile(Explorer.RootPath) is not { } path)
        {
            return;
        }

        // The save dialog does not ask to overwrite: an existing file is opened, never emptied.
        if (File.Exists(path))
        {
            _feedback.Show(new ResultError(FeedbackType.Info, Strings.Toast_FileExists_Title, string.Format(CultureInfo.CurrentCulture, FileExistsFormat, Path.GetFileName(path))));
            await OpenPathAsync(path, ct);
            return;
        }

        var created = await _files.CreateAsync(path, ct);
        if (!created.Ok)
        {
            _feedback.Show(created.Error!);
            return;
        }

        await OpenPathAsync(path, ct);
        await SetModeAsync(DocumentMode.Editing, ct);
    }

    [RelayCommand]
    private async Task OpenFileAsync(CancellationToken ct)
    {
        if (_dialogs.PickMarkdownFile() is { } path)
        {
            await OpenPathAsync(path, ct);
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync(CancellationToken ct)
    {
        if (_dialogs.PickFolder() is { } path)
        {
            await LoadFolderAsync(path, ct);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveActive))]
    private async Task SaveAsync(CancellationToken ct)
    {
        if (ActiveDocument is { } document)
        {
            await document.SaveAsync(ct);
        }
    }

    private bool CanSaveActive() => CanSave(ActiveDocument);

    [RelayCommand]
    private async Task CloseTabAsync(DocumentViewModel? document, CancellationToken ct)
    {
        if ((document ?? ActiveDocument) is { } target)
        {
            await TryCloseAsync(target, ct);
        }
    }

    [RelayCommand]
    private void ActivateTab(DocumentViewModel? document)
    {
        if (document is not null)
        {
            ActiveDocument = document;
        }
    }

    [RelayCommand]
    private void NextTab() => StepTab(1);

    [RelayCommand]
    private void PreviousTab() => StepTab(-1);

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ToggleMode() => SetModeCommand.Execute(IsEditing ? DocumentMode.Reading : DocumentMode.Editing);

    [RelayCommand(CanExecute = nameof(CanSetMode))]
    private async Task SetModeAsync(DocumentMode mode, CancellationToken ct)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        await Task.WhenAll(Documents.Select(d => d.ApplyModeAsync(mode, ct)));
        if (mode != DocumentMode.Reading)
        {
            ActiveDocument?.Surface?.Focus();
        }
    }

    private bool CanSetMode(DocumentMode mode) => HasDocument || mode == DocumentMode.Reading;

    [RelayCommand]
    private async Task ToggleExplorerAsync(CancellationToken ct)
    {
        IsExplorerVisible = !IsExplorerVisible;
        if (IsNarrow)
        {
            IsOutlineVisible = IsOutlineVisible && !IsExplorerVisible;
            return;
        }

        await _preferences.UpdateAsync(p => p with { ExplorerVisible = IsExplorerVisible }, ct);
    }

    [RelayCommand]
    private async Task ToggleOutlineAsync(CancellationToken ct)
    {
        IsOutlineVisible = !IsOutlineVisible;
        if (IsNarrow)
        {
            IsExplorerVisible = IsExplorerVisible && !IsOutlineVisible;
            return;
        }

        await _preferences.UpdateAsync(p => p with { OutlineVisible = IsOutlineVisible }, ct);
    }

    [RelayCommand]
    private Task ToggleThemeAsync(CancellationToken ct) => _theme.ToggleAsync(ct);

    [RelayCommand]
    private Task ZoomInAsync() => ChangeZoomAsync(AppConfig.ZoomStep);

    [RelayCommand]
    private Task ZoomOutAsync() => ChangeZoomAsync(-AppConfig.ZoomStep);

    [RelayCommand]
    private Task ZoomResetAsync() => ChangeZoomAsync(1.0 - Zoom);

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void OpenSearch() => ActiveDocument?.OpenSearch();

    [RelayCommand]
    private void Escape() => ActiveDocument?.CloseSearch();

    [RelayCommand]
    private async Task FormatAsync(FormatAction action, CancellationToken ct)
    {
        if (ActiveDocument is not { Surface: { } surface } document)
        {
            return;
        }

        switch (action)
        {
            case FormatAction.Link:
                var link = _linkFactory();
                if (await _dialogs.ShowContentAsync(Strings.Link_Title, link, Strings.Link_Insert, ct) && link.CanConfirm)
                {
                    surface.ApplyFormat(FormatAction.Link, link.Url.Trim());
                }

                break;
            case FormatAction.Image:
                var folder = Path.GetDirectoryName(document.FilePath) ?? string.Empty;
                if (_dialogs.PickImage(folder) is { } image)
                {
                    var relative = Path.GetRelativePath(folder, image).Replace('\\', '/').Replace(" ", "%20", StringComparison.Ordinal);
                    surface.ApplyFormat(FormatAction.Image, _assets.GetFileUrl(image), relative);
                }

                break;
            default:
                surface.ApplyFormat(action);
                break;
        }

        surface.Focus();
    }

    private async Task LoadFolderAsync(string path, CancellationToken ct)
    {
        if (!await Explorer.LoadAsync(path, ct))
        {
            return;
        }

        foreach (var document in Documents)
        {
            document.UpdateRoot(Explorer.RootPath);
        }

        if (!IsExplorerVisible)
        {
            IsExplorerVisible = true;
            if (IsNarrow)
            {
                IsOutlineVisible = false;
            }
        }

        await _preferences.UpdateAsync(p => p with { LastFolder = Explorer.RootPath, ExplorerVisible = IsNarrow ? p.ExplorerVisible : true }, ct);
    }

    private async Task ChangeZoomAsync(double delta)
    {
        var zoom = Math.Round(Math.Clamp(Zoom + delta, AppConfig.ZoomMin, AppConfig.ZoomMax), 1);
        if (Math.Abs(zoom - Zoom) < 0.001)
        {
            return;
        }

        Zoom = zoom;
        foreach (var document in Documents)
        {
            document.ApplyZoom(zoom);
        }

        await _preferences.UpdateAsync(p => p with { Zoom = zoom }, CancellationToken.None);
    }

    private void StepTab(int step)
    {
        if (Documents.Count == 0)
        {
            return;
        }

        var index = ActiveDocument is null ? 0 : Documents.IndexOf(ActiveDocument);
        ActiveDocument = Documents[(index + step + Documents.Count) % Documents.Count];
    }

    /// <summary>Removes a tab, activating its right neighbour, else its left one.</summary>
    private void Remove(DocumentViewModel document)
    {
        var index = Documents.IndexOf(document);
        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = index + 1 < Documents.Count ? Documents[index + 1] : index > 0 ? Documents[index - 1] : null;
        }

        document.OpenFileRequested -= OnOpenFileRequested;
        document.LinkRequested -= OnLinkRequested;
        document.OutlineNavigated -= OnOutlineNavigated;
        document.Abandon();
        Documents.Remove(document);
        if (!Documents.Any(d => string.Equals(d.FilePath, document.FilePath, StringComparison.OrdinalIgnoreCase)))
        {
            _watcher.UnwatchFile(document.FilePath);
        }
    }

    private void CloseExplorerOverlay()
    {
        if (IsNarrow)
        {
            IsExplorerVisible = false;
        }
    }

    private void OnOpenFileRequested(object? sender, string path) => _ = OpenRequestedAsync(path);

    private async Task OpenRequestedAsync(string path)
    {
        try
        {
            await OpenPathAsync(path, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Not an error (rules/errors.md).
        }
    }

    private void OnLinkRequested(object? sender, EventArgs e) => FormatCommand.Execute(FormatAction.Link);

    private void ToggleFormat(bool requested, bool current, FormatAction action, string property)
    {
        if (requested != current)
        {
            FormatCommand.Execute(action);
        }

        OnPropertyChanged(property);
    }

    private void OnOutlineNavigated(object? sender, EventArgs e)
    {
        if (IsNarrow)
        {
            IsOutlineVisible = false;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (var document in Documents)
        {
            document.ApplyTheme(_theme.PageTheme);
        }
    }

    private void OnFileChanged(object? sender, FileChange change)
    {
        // Background source: FileSystemWatcher timer thread.
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (var document in Documents.Where(d => string.Equals(d.FilePath, change.Path, StringComparison.OrdinalIgnoreCase)))
            {
                document.OnFileChanged(change);
            }
        });
    }

    private void OnActiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.IsModified))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(DocumentViewModel.FormatState))
        {
            foreach (var name in FormatProperties)
            {
                OnPropertyChanged(name);
            }
        }
    }
}
