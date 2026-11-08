using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MdStudio.Helpers;
using MdStudio.Models;
using MdStudio.Resources;
using MdStudio.Services;
using Microsoft.Extensions.Logging;

namespace MdStudio.ViewModels;

/// <summary>One opened document (one tab): file, edition state, outline, search, disk changes.</summary>
/// <param name="files">File access.</param>
/// <param name="renderer">Markdown rendering.</param>
/// <param name="composer">Markdown recomposition.</param>
/// <param name="launcher">Browser launcher.</param>
/// <param name="feedback">Notifications.</param>
/// <param name="logger">Logger.</param>
public sealed partial class DocumentViewModel(
    ITextFileService files,
    IMarkdownRenderService renderer,
    IMarkdownComposeService composer,
    IExternalLauncher launcher,
    IFeedbackService feedback,
    ILogger<DocumentViewModel> logger) : ObservableObject
{
    private static readonly CompositeFormat SearchCounterFormat = CompositeFormat.Parse(Strings.Search_Counter);
    private static readonly CompositeFormat SavedFormat = CompositeFormat.Parse(Strings.Notify_Saved);
    private static readonly CompositeFormat NotFoundFormat = CompositeFormat.Parse(Strings.Error_NotFound);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Message de page ignore {Type}")]
    private static partial void LogPageMessageIgnored(ILogger logger, string type);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lien local non Markdown ignore {Target}")]
    private static partial void LogLocalLinkIgnored(ILogger logger, string target);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Page du document {Name} affichee en {ElapsedMs} ms")]
    private static partial void LogPageShown(ILogger logger, string name, double elapsedMs);

    private const int MinSearchLength = 2;

    private readonly TaskCompletionSource _content = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private long _loadStarted;
    private Task _modeTransition = Task.CompletedTask;
    private string _baseText = string.Empty;
    private RenderedDocument _rendered = new(string.Empty, string.Empty, []);
    private DocumentPageState? _state;
    private DateTime _diskWriteUtc;
    private long _diskLength;

    /// <summary>Raised when a link of the document targets another Markdown file.</summary>
    public event EventHandler<string>? OpenFileRequested;

    /// <summary>Raised when the page asks for the link dialog (<c>Ctrl+K</c>).</summary>
    public event EventHandler? LinkRequested;

    /// <summary>Raised when the user picked an outline entry.</summary>
    public event EventHandler? OutlineNavigated;

    /// <summary>Raised when the search field must take the focus with its text selected.</summary>
    public event EventHandler? SearchFocusRequested;

    /// <summary>Gets the tab identifier shared with the page.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets the page of the tab, once the view attached it.</summary>
    public IDocumentSurface? Surface { get; private set; }

    /// <summary>Gets the text last saved or loaded.</summary>
    public string SavedText { get; private set; } = string.Empty;

    /// <summary>Gets the on-disk format.</summary>
    public TextFormat Format { get; private set; } = new(TextEncodingKind.Utf8, LineEnding.CrLf);

    /// <summary>Gets the headings of the outline (levels 1 to 3).</summary>
    public ObservableCollection<OutlineEntry> Outline { get; } = [];

    /// <summary>Gets the absolute path.</summary>
    [ObservableProperty]
    public partial string FilePath { get; private set; } = string.Empty;

    /// <summary>Gets the file name.</summary>
    [ObservableProperty]
    public partial string FileName { get; private set; } = string.Empty;

    /// <summary>Gets the path relative to the opened folder, with backslashes (tab tooltip).</summary>
    [ObservableProperty]
    public partial string RelativePath { get; private set; } = string.Empty;

    /// <summary>Gets the folders of the breadcrumb, relative to the opened folder.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Breadcrumb { get; private set; } = [];

    /// <summary>Gets the encoding label of the status bar.</summary>
    [ObservableProperty]
    public partial string EncodingText { get; private set; } = string.Empty;

    /// <summary>Gets the line ending label of the status bar.</summary>
    [ObservableProperty]
    public partial string LineEndingText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the content differs from the saved text.</summary>
    [ObservableProperty]
    public partial bool IsModified { get; private set; }

    /// <summary>Gets a value indicating whether the page is rendering the document (loading screen).</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>Gets the word count.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordCountText))]
    public partial int WordCount { get; private set; }

    /// <summary>Gets the anchor of the current heading.</summary>
    [ObservableProperty]
    public partial string? CurrentAnchor { get; private set; }

    /// <summary>Gets the formatting at the caret.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlockStyleIndex))]
    public partial PageFormatState FormatState { get; private set; } = new();

    /// <summary>Gets a value indicating whether the search bar is open.</summary>
    [ObservableProperty]
    public partial bool IsSearchOpen { get; private set; }

    /// <summary>Gets or sets the searched text.</summary>
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    /// <summary>Gets the search counter ("n sur N", "Aucun résultat" or empty).</summary>
    [ObservableProperty]
    public partial string SearchCounterText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the file changed or disappeared on disk.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiskBannerText))]
    public partial bool IsDiskChanged { get; private set; }

    /// <summary>Gets a value indicating whether the file was deleted on disk.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DiskBannerText), nameof(CanReload))]
    public partial bool IsDiskDeleted { get; private set; }

    /// <summary>Gets the word count label ("N mots").</summary>
    public string WordCountText => FrenchPlural.Words(WordCount);

    /// <summary>Gets the disk banner text.</summary>
    public string DiskBannerText => IsDiskDeleted ? Strings.Banner_Deleted : Strings.Banner_Changed;

    /// <summary>Gets a value indicating whether the file can be reloaded from disk.</summary>
    public bool CanReload => !IsDiskDeleted;

    /// <summary>Gets the index of the current block style: paragraph, heading 1 to 3, quote.</summary>
    public int BlockStyleIndex => FormatState.Block switch
    {
        "h1" => 1,
        "h2" => 2,
        "h3" => 3,
        "blockquote" => 4,
        _ => 0,
    };

    /// <summary>Prepares the tab of a file still being read: name, path and loading screen.</summary>
    /// <param name="path">Absolute path.</param>
    /// <param name="state">Window-wide page settings.</param>
    /// <param name="root">Opened folder, used for the relative path and the breadcrumb.</param>
    public void Prepare(string path, DocumentPageState state, string? root)
    {
        FilePath = path;
        FileName = Path.GetFileName(path);
        _state = state;
        UpdateRoot(root);
        StartLoading();
    }

    /// <summary>Provides the read and rendered file; the page loads it once attached.</summary>
    /// <param name="file">The file.</param>
    /// <param name="rendered">Its rendering.</param>
    public void Show(TextFile file, RenderedDocument rendered)
    {
        Accept(file, rendered);
        _content.TrySetResult();
    }

    /// <summary>Gives up a file that could not be read: a pending page attachment is cancelled.</summary>
    public void Abandon() => _content.TrySetCanceled();

    /// <summary>Updates the relative path and the breadcrumb for a new opened folder.</summary>
    /// <param name="root">Opened folder, or null.</param>
    public void UpdateRoot(string? root)
    {
        var relative = root is not null && FilePath.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)
            ? Path.GetRelativePath(root, FilePath)
            : FileName;
        RelativePath = relative;
        Breadcrumb = relative.Split('\\')[..^1];
    }

    /// <summary>
    /// Attaches the page of the tab once the file is read, then loads the document into it. Mode changes
    /// made meanwhile only update the settings the page loads with.
    /// </summary>
    /// <param name="surface">Page of the tab.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when the document is sent to the page; cancelled when the file is abandoned.</returns>
    public async Task AttachSurfaceAsync(IDocumentSurface surface, CancellationToken ct)
    {
        await _content.Task.WaitAsync(ct);
        Surface = surface;
        if (_state is not null)
        {
            await surface.LoadAsync(_rendered.Html, _state, ct);
            if (_state.Mode == DocumentMode.Source)
            {
                surface.ShowSource(_baseText);
            }
        }
    }

    /// <summary>
    /// Applies the window mode. Transitions run one after the other: entering the source mode sends the
    /// recomposed text to the page, leaving it renders the typed text again when it changed.
    /// </summary>
    /// <param name="mode">New mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when the page shows the new mode.</returns>
    public Task ApplyModeAsync(DocumentMode mode, CancellationToken ct)
    {
        _modeTransition = TransitionAfterAsync(_modeTransition, mode, ct);
        return _modeTransition;
    }

    /// <summary>Applies the zoom factor.</summary>
    /// <param name="zoom">New factor.</param>
    public void ApplyZoom(double zoom)
    {
        _state = _state is null ? null : _state with { Zoom = zoom };
        Surface?.SetZoom(zoom);
    }

    /// <summary>Applies the theme.</summary>
    /// <param name="theme">New theme.</param>
    public void ApplyTheme(PageTheme theme)
    {
        _state = _state is null ? null : _state with { Theme = theme };
        Surface?.SetTheme(theme);
    }

    /// <summary>Opens the search bar and asks the view to focus it.</summary>
    public void OpenSearch()
    {
        IsSearchOpen = true;
        SearchFocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Handles a message of the page.</summary>
    /// <param name="message">Message of this tab.</param>
    public void HandlePageMessage(PageMessage message)
    {
        switch (message.Type)
        {
            case "changed":
                UpdateStats(message);
                if (message.Blocks is { } blocks)
                {
                    IsModified = !string.Equals(Compose(blocks), SavedText, StringComparison.Ordinal);
                }

                break;
            case "stats":
                UpdateStats(message);
                break;
            case "sourceChanged":
                IsModified = !string.Equals(LineEndings.Normalize(message.Source ?? string.Empty, Format.LineEnding), SavedText, StringComparison.Ordinal);
                break;
            case "rendered":
                IsLoading = false;
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    var elapsedMs = Stopwatch.GetElapsedTime(_loadStarted).TotalMilliseconds;
                    LogPageShown(logger, FileName, elapsedMs);
                }

                break;
            case "currentHeading":
                CurrentAnchor = message.Heading;
                break;
            case "searchResult":
                SearchCounterText = message.Total switch
                {
                    null => string.Empty,
                    0 => Strings.Search_NoResult,
                    var total => string.Format(CultureInfo.CurrentCulture, SearchCounterFormat, message.Current ?? 0, total),
                };
                break;
            case "formatState":
                FormatState = message.Format ?? new PageFormatState();
                break;
            case "link":
                OpenLink(message.Href);
                break;
            case "requestLink":
                LinkRequested?.Invoke(this, EventArgs.Empty);
                break;
            case "notify" when message.Key == "mermaidValidated":
                feedback.Show(new ResultError(FeedbackType.Info, Strings.Notify_DiagramValidated));
                break;
            default:
                LogPageMessageIgnored(logger, message.Type);
                break;
        }
    }

    /// <summary>Saves the document into its file (format kept) and notifies the user.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when written.</returns>
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        if (Surface is null)
        {
            return false;
        }

        // A mode change in progress decides which zone of the page holds the text.
        await _modeTransition.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        var text = _state?.Mode == DocumentMode.Source
            ? LineEndings.Normalize(await Surface.GetSourceAsync(ct), Format.LineEnding)
            : Compose(await Surface.SerializeAsync(ct));
        var result = await files.WriteAsync(FilePath, text, Format, ct);
        if (!result.Ok)
        {
            feedback.Show(result.Error!);
            return false;
        }

        SavedText = text;
        _diskWriteUtc = result.Data!.LastWriteUtc;
        _diskLength = result.Data.Length;
        IsModified = false;
        IsDiskChanged = false;
        IsDiskDeleted = false;
        feedback.Show(new ResultError(FeedbackType.Info, string.Format(CultureInfo.CurrentCulture, SavedFormat, FileName)));
        return true;
    }

    /// <summary>Reacts to a change of the file on disk; the application's own writes are ignored.</summary>
    /// <param name="change">Observed change.</param>
    public void OnFileChanged(FileChange change)
    {
        if (change.Kind == FileChangeKind.Deleted || !File.Exists(FilePath))
        {
            IsDiskDeleted = true;
            IsDiskChanged = true;
            return;
        }

        var info = new FileInfo(FilePath);
        if (info.LastWriteTimeUtc == _diskWriteUtc && info.Length == _diskLength)
        {
            return;
        }

        IsDiskDeleted = false;
        IsDiskChanged = true;
    }

    /// <summary>Closes the search bar and removes the highlighting.</summary>
    [RelayCommand]
    public void CloseSearch()
    {
        if (!IsSearchOpen && SearchQuery.Length == 0)
        {
            return;
        }

        IsSearchOpen = false;
        SearchQuery = string.Empty;
        SearchCounterText = string.Empty;
        Surface?.ClearSearch();
    }

    partial void OnSearchQueryChanged(string value)
    {
        var query = value.Trim();
        if (query.Length >= MinSearchLength)
        {
            Surface?.Search(query);
        }
        else
        {
            SearchCounterText = string.Empty;
            Surface?.ClearSearch();
        }
    }

    [RelayCommand]
    private void SearchNext() => Surface?.SearchStep(1);

    [RelayCommand]
    private void SearchPrevious() => Surface?.SearchStep(-1);

    [RelayCommand]
    private void NavigateOutline(OutlineEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        Surface?.ScrollTo(entry.Anchor);
        OutlineNavigated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ReloadFromDiskAsync(CancellationToken ct)
    {
        var read = await files.ReadAsync(FilePath, ct);
        if (!read.Ok)
        {
            feedback.Show(read.Error!);
            return;
        }

        var rendered = await renderer.RenderAsync(read.Data!, ct);
        Accept(read.Data!, rendered);
        IsModified = false;
        IsDiskChanged = false;
        IsDiskDeleted = false;
        if (Surface is not null && _state is not null)
        {
            StartLoading();
            await Surface.LoadAsync(rendered.Html, _state, ct);
            if (_state.Mode == DocumentMode.Source)
            {
                Surface.ShowSource(_baseText);
            }
        }
    }

    private async Task TransitionAfterAsync(Task previous, DocumentMode mode, CancellationToken ct)
    {
        // A failed or cancelled transition was already reported to its own caller.
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        var leaving = _state?.Mode;
        _state = _state is null ? null : _state with { Mode = mode };
        CloseSearch();
        if (Surface is not { } surface)
        {
            return;
        }

        if (mode == DocumentMode.Source)
        {
            if (leaving != DocumentMode.Source)
            {
                surface.ShowSource(Compose(await surface.SerializeAsync(ct)));
            }

            return;
        }

        if (leaving == DocumentMode.Source && _state is not null)
        {
            var text = LineEndings.Normalize(await surface.GetSourceAsync(ct), Format.LineEnding);
            if (!string.Equals(text, _baseText, StringComparison.Ordinal))
            {
                // The typed text becomes the base of the block recomposition; the saved text is unchanged.
                var rendered = await renderer.RenderAsync(new TextFile(FilePath, text, Format, _diskWriteUtc, _diskLength), ct);
                _baseText = text;
                _rendered = rendered;
                StartLoading();
                await surface.LoadAsync(rendered.Html, _state, ct);
                return;
            }
        }

        surface.SetMode(mode);
    }

    private void StartLoading()
    {
        IsLoading = true;
        _loadStarted = Stopwatch.GetTimestamp();
    }

    [RelayCommand]
    private void KeepMyVersion()
    {
        IsDiskChanged = false;
    }

    private void Accept(TextFile file, RenderedDocument rendered)
    {
        _baseText = file.Text;
        _rendered = rendered;
        SavedText = file.Text;
        Format = file.Format;
        _diskWriteUtc = file.LastWriteUtc;
        _diskLength = file.Length;
        EncodingText = file.Format.Encoding switch
        {
            TextEncodingKind.Utf8Bom => Strings.Encoding_Utf8Bom,
            TextEncodingKind.Utf16Le => Strings.Encoding_Utf16Le,
            TextEncodingKind.Utf16Be => Strings.Encoding_Utf16Be,
            _ => Strings.Encoding_Utf8,
        };
        LineEndingText = file.Format.LineEnding == LineEnding.CrLf ? "CRLF" : "LF";
    }

    private string Compose(IEnumerable<PageBlock> blocks) =>
        Compose(blocks.Select(b => new EditedBlock(b.Src, b.Md)).ToList());

    private string Compose(IReadOnlyList<EditedBlock> blocks) =>
        composer.Compose(_baseText, _rendered, blocks, Format.LineEnding);

    private void UpdateStats(PageMessage message)
    {
        if (message.Words is int words)
        {
            WordCount = words;
        }

        if (message.Outline is { } outline)
        {
            Outline.Clear();
            foreach (var heading in outline)
            {
                Outline.Add(new OutlineEntry(heading.Level, heading.Text, heading.Id));
            }

            // The rebuilt list lost its selection: re-apply the current heading.
            OnPropertyChanged(nameof(CurrentAnchor));
        }
    }

    private void OpenLink(string? href)
    {
        if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#'))
        {
            return;
        }

        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute) && absolute.Scheme != Uri.UriSchemeFile)
        {
            var opened = launcher.Open(href);
            if (!opened.Ok)
            {
                feedback.Show(opened.Error!);
            }

            return;
        }

        // E-09: the target is resolved from the document folder, read-only, Markdown extensions only.
        var relative = Uri.UnescapeDataString(href.Split('#', '?')[0]);
        var folder = Path.GetDirectoryName(FilePath) ?? string.Empty;
        var target = absolute?.LocalPath ?? Path.GetFullPath(Path.Combine(folder, relative));
        if (!AppConfig.MarkdownExtensions.Contains(Path.GetExtension(target), StringComparer.OrdinalIgnoreCase))
        {
            LogLocalLinkIgnored(logger, target);
            return;
        }

        if (!File.Exists(target))
        {
            feedback.Show(new ResultError(FeedbackType.Danger, string.Format(CultureInfo.CurrentCulture, NotFoundFormat, Path.GetFileName(target))));
            return;
        }

        OpenFileRequested?.Invoke(this, target);
    }
}
