using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MdStudio.Models;
using MdStudio.ViewModels;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MdStudio.Views;

/// <summary>Main window. Code-behind limited to view plumbing: presenters, layout, keys, drop, closing.</summary>
public partial class MainWindow : FluentWindow
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Echec de la fermeture de la fenetre")]
    private static partial void LogCloseFailed(ILogger logger, Exception? exception);

    private readonly ShellViewModel _shell;
    private readonly ILogger<MainWindow> _logger;
    private bool _closeConfirmed;

    /// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
    /// <param name="shell">Shell ViewModel.</param>
    /// <param name="snackbars">WPF UI snackbar service.</param>
    /// <param name="dialogs">WPF UI dialog service.</param>
    /// <param name="logger">Logger.</param>
    public MainWindow(ShellViewModel shell, ISnackbarService snackbars, IContentDialogService dialogs, ILogger<MainWindow> logger)
    {
        _shell = shell;
        _logger = logger;
        InitializeComponent();
        DataContext = shell;

        // TitleBar.Header content is realized in the control template and inherits no DataContext (rules/xaml.md).
        Tabs.DataContext = shell;
        snackbars.SetSnackbarPresenter(NotificationPresenter);
        dialogs.SetDialogHost(DialogHost);

        shell.PropertyChanged += OnShellPropertyChanged;
        SizeChanged += (_, e) => shell.UpdateWidth(e.NewSize.Width);
        ExplorerSplitter.DragCompleted += OnSplitterDragCompleted;
        ExplorerSplitter.KeyUp += (_, _) => CommitExplorerWidth();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
        AllowDrop = true;
        PreviewDragOver += OnPreviewDragOver;
        PreviewDrop += OnPreviewDrop;
        Closing += OnClosing;
        UpdateExplorerColumn();
    }

    /// <summary>Restores the saved window bounds when they are still on a screen.</summary>
    /// <param name="bounds">Saved bounds, or null to keep the default centered window.</param>
    /// <param name="maximized">True to maximize.</param>
    public void RestorePlacement(WindowBounds? bounds, bool maximized)
    {
        if (bounds is not null
            && bounds.Left + bounds.Width > SystemParameters.VirtualScreenLeft
            && bounds.Top + bounds.Height > SystemParameters.VirtualScreenTop
            && bounds.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
            && bounds.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = Math.Max(bounds.Width, MinWidth);
            Height = Math.Max(bounds.Height, MinHeight);
        }

        if (maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static string? KeyName(Key key) => key switch
    {
        >= Key.A and <= Key.Z => key.ToString(),
        Key.Tab => "Tab",
        Key.OemPlus or Key.Add => "Plus",
        Key.OemMinus or Key.Subtract => "Minus",
        Key.D0 or Key.NumPad0 => "0",
        Key.Escape => "Escape",
        _ => null,
    };

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellViewModel.ExplorerWidth) or nameof(ShellViewModel.IsExplorerDocked))
        {
            UpdateExplorerColumn();
        }
    }

    private void UpdateExplorerColumn()
    {
        if (_shell.IsExplorerDocked)
        {
            ExplorerColumn.MinWidth = AppConfig.ExplorerWidthMin;
            ExplorerColumn.MaxWidth = AppConfig.ExplorerWidthMax;
            ExplorerColumn.Width = new GridLength(_shell.ExplorerWidth);
        }
        else
        {
            ExplorerColumn.MinWidth = 0;
            ExplorerColumn.Width = new GridLength(0);
        }
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e) => CommitExplorerWidth();

    private void CommitExplorerWidth()
    {
        if (_shell.IsExplorerDocked)
        {
            _ = _shell.CommitExplorerWidthAsync(ExplorerColumn.ActualWidth, CancellationToken.None);
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (DialogHost.IsKeyboardFocusWithin || KeyName(key) is not { } name)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;
        if (_shell.HandleShortcut(name, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift)))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && DocumentArea.IsMouseOver)
        {
            _shell.ZoomByWheel(e.Delta);
            e.Handled = true;
        }
    }

    private void OnPreviewDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            e.Handled = true;
            _ = OpenDroppedAsync(paths);
        }
    }

    private async Task OpenDroppedAsync(string[] paths)
    {
        try
        {
            await _shell.OpenPathsAsync(paths, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Not an error (rules/errors.md).
        }
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_closeConfirmed)
        {
            return;
        }

        e.Cancel = true;
        _ = ConfirmCloseAsync();
    }

    private async Task ConfirmCloseAsync()
    {
        try
        {
            if (!await _shell.RequestWindowCloseAsync(CancellationToken.None))
            {
                return;
            }

            var bounds = RestoreBounds.IsEmpty ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            await _shell.SaveWindowPlacementAsync(
                new WindowBounds(bounds.Left, bounds.Top, bounds.Width, bounds.Height),
                WindowState == WindowState.Maximized,
                CancellationToken.None);
            _closeConfirmed = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            // Closing was cancelled: the window stays open.
        }
        catch (InvalidOperationException ex)
        {
            LogCloseFailed(_logger, ex);
        }
    }
}
