using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using MdStudio.Models;
using MdStudio.ViewModels;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace MdStudio.Views.Controls;

/// <summary>
/// Single WebView2 hosting the container page and one iframe per tab. View plumbing only: hardening,
/// resource serving, message routing; every decision is taken by <see cref="ShellViewModel"/>.
/// </summary>
public sealed class DocumentHost : ContentControl, IDisposable
{
    private static readonly HashSet<string> BlockedMenuItems = new(StringComparer.Ordinal)
    {
        "back", "forward", "reload", "saveAs", "print", "share", "webCapture", "createQrCode",
        "sendTabToDevices", "inspectElement", "openLinkInNewWindow", "saveLinkAs", "saveImageAs",
    };

    private readonly WebView2CompositionControl _web = new();
    private readonly Dictionary<string, FrameSurface> _surfaces = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _hostReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ShellViewModel? _shell;
    private CoreWebView2Environment? _environment;
    private bool _initializing;

    /// <summary>Initializes a new instance of the <see cref="DocumentHost"/> class.</summary>
    public DocumentHost()
    {
        Content = _web;
        Focusable = false;
        DataContextChanged += (_, _) => AttachShell(DataContext as ShellViewModel);
        Loaded += OnLoaded;
        Dispatcher.ShutdownStarted += (_, _) => Dispose();
    }

    private static string Origin => $"https://{AppConfig.VirtualHostName}/";

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException)
        {
            _shell?.ReportPageFailure(ex);
        }
    }

    private async Task InitializeAsync()
    {
        if (_initializing || _shell is null)
        {
            return;
        }

        _initializing = true;
        _environment = await CoreWebView2Environment.CreateAsync(
            null, _shell.WebViewDataFolder, new CoreWebView2EnvironmentOptions { Language = "fr-FR" });
        await _web.EnsureCoreWebView2Async(_environment);

        var core = _web.CoreWebView2;
        var settings = core.Settings;
#if DEBUG
        settings.AreDevToolsEnabled = true;
#else
        settings.AreDevToolsEnabled = false;
#endif
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = true;
        settings.IsZoomControlEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsSwipeNavigationEnabled = false;

        // SmartScreen looks up every document navigation online: on the virtual host it waits for a
        // 2-second timeout before each page (host and every tab) and contacts the network for nothing.
        settings.IsReputationCheckingRequired = false;

        core.AddWebResourceRequestedFilter(Origin + "*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.NavigationStarting += (_, args) => args.Cancel = !IsAllowed(args.Uri);
        core.FrameNavigationStarting += (_, args) => args.Cancel = !IsAllowed(args.Uri);
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.WebMessageReceived += OnWebMessageReceived;
        core.ContextMenuRequested += OnContextMenuRequested;
        core.Navigate(_shell.HostPageUrl);
    }

    private static bool IsAllowed(string uri) =>
        uri.StartsWith(Origin, StringComparison.OrdinalIgnoreCase) || uri == "about:blank";

    private void AttachShell(ShellViewModel? shell)
    {
        if (ReferenceEquals(shell, _shell))
        {
            return;
        }

        if (_shell is not null)
        {
            _shell.Documents.CollectionChanged -= OnDocumentsChanged;
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = shell;
        if (_shell is null)
        {
            return;
        }

        _shell.Documents.CollectionChanged += OnDocumentsChanged;
        _shell.PropertyChanged += OnShellPropertyChanged;
        foreach (var document in _shell.Documents)
        {
            _ = AttachAsync(document);
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var document in e.NewItems?.OfType<DocumentViewModel>() ?? [])
        {
            _ = AttachAsync(document);
        }

        foreach (var document in e.OldItems?.OfType<DocumentViewModel>() ?? [])
        {
            _surfaces.Remove(document.Id);
            Post(new HostMessage { Type = "close", Tab = document.Id });
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveDocument) && _shell?.ActiveDocument is { } active)
        {
            Post(new HostMessage { Type = "activate", Tab = active.Id });
        }
    }

    private async Task AttachAsync(DocumentViewModel document)
    {
        var surface = new FrameSurface(this, document.Id);
        _surfaces[document.Id] = surface;
        try
        {
            await _hostReady.Task;
            Post(new HostMessage { Type = "open", Tab = document.Id });
            if (ReferenceEquals(_shell?.ActiveDocument, document))
            {
                Post(new HostMessage { Type = "activate", Tab = document.Id });
            }

            await document.AttachSurfaceAsync(surface, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // The tab was closed before its page was ready.
        }
    }

    private void Post(HostMessage message)
    {
        if (_web.CoreWebView2 is { } core && _hostReady.Task.IsCompleted)
        {
            core.PostWebMessageAsJson(JsonSerializer.Serialize(message, PageJsonContext.Default.HostMessage));
        }
    }

    private async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (_shell is null || _environment is null)
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var asset = await _shell.ResolvePageResourceAsync(new Uri(e.Request.Uri), CancellationToken.None);

            // No disk cache: the page files of a new version must never be served from an old profile.
            e.Response = asset is null
                ? _environment.CreateWebResourceResponse(null, 404, "Not Found", string.Empty)
                : _environment.CreateWebResourceResponse(asset.Content, 200, "OK", $"Content-Type: {asset.ContentType}\r\nCache-Control: no-store");
        }
        catch (Exception ex) when (ex is UriFormatException or IOException or COMException)
        {
            _shell.ReportPageFailure(ex);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        PageMessage? message;
        try
        {
            message = JsonSerializer.Deserialize(e.WebMessageAsJson, PageJsonContext.Default.PageMessage);
        }
        catch (JsonException ex)
        {
            _shell?.ReportPageFailure(new PageProtocolException("Message de page illisible", ex));
            return;
        }

        if (message is null || _shell is null)
        {
            return;
        }

        var surface = message.Tab is { } tab ? _surfaces.GetValueOrDefault(tab) : null;
        switch (message.Type)
        {
            case "hostReady":
                _hostReady.TrySetResult();
                break;
            case "ready":
                surface?.MarkReady();
                break;
            case "serialized":
                surface?.Complete(message);
                break;
            case "drop":
                var paths = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).ToList() ?? [];
                if (paths.Count > 0)
                {
                    _ = OpenDroppedAsync(paths);
                }

                break;
            default:
                _shell.HandlePageMessage(message);
                break;
        }
    }

    private async Task OpenDroppedAsync(IReadOnlyList<string> paths)
    {
        try
        {
            await _shell!.OpenPathsAsync(paths, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Not an error (rules/errors.md).
        }
    }

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        var items = e.MenuItems;
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (BlockedMenuItems.Contains(items[i].Name))
            {
                items.RemoveAt(i);
            }
        }
    }

    /// <summary>Releases the WebView2 control (called when the dispatcher shuts down).</summary>
    public void Dispose() => _web.Dispose();

    private void FocusPage() => _web.Focus();

    /// <summary>Page of one tab: an iframe of the container page.</summary>
    private sealed class FrameSurface(DocumentHost host, string tab) : IDocumentSurface
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<int, TaskCompletionSource<PageMessage>> _pending = [];
        private int _nextRequest;

        public void MarkReady() => _ready.TrySetResult();

        public void Complete(PageMessage message)
        {
            if (message.RequestId is int id && _pending.Remove(id, out var completion))
            {
                completion.TrySetResult(message);
            }
        }

        public async Task LoadAsync(string html, DocumentPageState state, CancellationToken ct)
        {
            await _ready.Task.WaitAsync(ct);
            host.Post(new HostMessage
            {
                Type = "load",
                Tab = tab,
                Html = html,
                Mode = state.Mode,
                Zoom = state.Zoom,
                Dark = state.Theme.IsDark,
                Variables = state.Theme.Variables,
            });
        }

        public void SetMode(DocumentMode mode) => Send(new HostMessage { Type = "setMode", Tab = tab, Mode = mode });

        public void SetZoom(double zoom) => Send(new HostMessage { Type = "setZoom", Tab = tab, Zoom = zoom });

        public void SetTheme(PageTheme theme) =>
            Send(new HostMessage { Type = "setTheme", Tab = tab, Dark = theme.IsDark, Variables = theme.Variables });

        public void ApplyFormat(FormatAction action, string? argument = null, string? extra = null) =>
            Send(new HostMessage { Type = "format", Tab = tab, Action = action, Argument = argument, Extra = extra });

        public void Search(string query) => Send(new HostMessage { Type = "search", Tab = tab, Query = query });

        public void SearchStep(int direction) => Send(new HostMessage { Type = "searchStep", Tab = tab, Direction = direction });

        public void ClearSearch() => Send(new HostMessage { Type = "searchClear", Tab = tab });

        public void ScrollTo(string anchor) => Send(new HostMessage { Type = "scrollTo", Tab = tab, Anchor = anchor });

        public void Focus()
        {
            host.FocusPage();
            Send(new HostMessage { Type = "focus", Tab = tab });
        }

        public void ShowSource(string text) => Send(new HostMessage { Type = "showSource", Tab = tab, Source = text });

        public async Task<IReadOnlyList<EditedBlock>> SerializeAsync(CancellationToken ct)
        {
            var reply = await RequestAsync("serialize", ct);
            return reply.Blocks?.Select(b => new EditedBlock(b.Src, b.Md)).ToList() ?? [];
        }

        public async Task<string> GetSourceAsync(CancellationToken ct)
        {
            var reply = await RequestAsync("getSource", ct);
            return reply.Source ?? string.Empty;
        }

        private async Task<PageMessage> RequestAsync(string type, CancellationToken ct)
        {
            await _ready.Task.WaitAsync(ct);
            var id = ++_nextRequest;
            var completion = new TaskCompletionSource<PageMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = completion;
            using var registration = ct.Register(() => completion.TrySetCanceled(ct));
            host.Post(new HostMessage { Type = type, Tab = tab, RequestId = id });
            return await completion.Task;
        }

        private void Send(HostMessage message) => _ = SendWhenReadyAsync(message);

        private async Task SendWhenReadyAsync(HostMessage message)
        {
            await _ready.Task;
            host.Post(message);
        }
    }
}
