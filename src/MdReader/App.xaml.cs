using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MdReader.Models;
using MdReader.Resources;
using MdReader.Services;
using MdReader.ViewModels;
using MdReader.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Wpf.Ui;

namespace MdReader;

/// <summary>Composition root: logging, global handlers, single instance, host, theme, main window.</summary>
public partial class App : Application
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Seconde instance : chemins transmis a la premiere")]
    private static partial void LogForwardedToFirstInstance(Microsoft.Extensions.Logging.ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "{App} {Version} demarre")]
    private static partial void LogStarted(Microsoft.Extensions.Logging.ILogger logger, string app, string version);

    private IHost? _host;
    private ILogger<App>? _logger;

    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    public App() => Startup += OnStartupEvent;

    // Tolerated async void (rules/threading.md): event handler whose body is guarded.
    private async void OnStartupEvent(object sender, StartupEventArgs e)
    {
        try
        {
            await StartAsync(e.Args).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Last boundary of the startup: any failure is logged and ends the process cleanly.
            Log.Fatal(ex, "Echec du demarrage");
            await Log.CloseAndFlushAsync().ConfigureAwait(true);
            Shutdown(1);
        }
    }

    /// <inheritdoc/>
    protected override void OnExit(ExitEventArgs e)
    {
        // Detached first: the global handlers must not reach a disposed container.
        // The host disposes its singletons (file watchers, single instance pipe).
        var host = _host;
        _host = null;
        host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static bool DebugEnabled() =>
        Environment.GetEnvironmentVariable($"{AppConfig.AppName.ToUpperInvariant()}_DEBUG") == "1";

    private static void ConfigureLogging(LoggerConfiguration configuration, IAppPaths paths)
    {
        configuration
            .MinimumLevel.Is(DebugEnabled() ? LogEventLevel.Debug : Enum.Parse<LogEventLevel>(AppConfig.LogLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                path: Path.Combine(paths.LogsFolder, "app-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: AppConfig.LogMaxBytes,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);
        if (DebugEnabled())
        {
            configuration.WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture);
        }
    }

    private static IHost BuildHost(IAppPaths paths) =>
        Host.CreateDefaultBuilder()
            .UseSerilog((_, _, configuration) => ConfigureLogging(configuration, paths))
            .ConfigureServices(services =>
            {
                services.AddSingleton(paths);
                services.AddSingleton<IPreferencesService, PreferencesService>();

                // WPF UI infrastructure services, used as shipped.
                services.AddSingleton<ISnackbarService, SnackbarService>();
                services.AddSingleton<IContentDialogService, ContentDialogService>();

                // Project services.
                services.AddSingleton<IFeedbackService, FeedbackService>();
                services.AddSingleton<IAppThemeService, AppThemeService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<ITextFileService, TextFileService>();
                services.AddSingleton<IPageAssetProvider, PageAssetProvider>();
                services.AddSingleton<IMarkdownRenderService, MarkdownRenderService>();
                services.AddSingleton<IMarkdownComposeService, MarkdownComposeService>();
                services.AddSingleton<IFolderScanService, FolderScanService>();
                services.AddSingleton<IFileWatchService, FileWatchService>();
                services.AddSingleton<ISingleInstanceService, SingleInstanceService>();
                services.AddSingleton<IExternalLauncher, ExternalLauncher>();

                // ViewModels and views.
                services.AddSingleton<ExplorerViewModel>();
                services.AddSingleton<ShellViewModel>();
                services.AddTransient<DocumentViewModel>();
                services.AddSingleton<Func<DocumentViewModel>>(provider => provider.GetRequiredService<DocumentViewModel>);
                services.AddTransient<LinkDialogViewModel>();
                services.AddSingleton<Func<LinkDialogViewModel>>(provider => provider.GetRequiredService<LinkDialogViewModel>);
                services.AddSingleton<MainWindow>();
            })
            .Build();

    private async Task StartAsync(string[] args)
    {
        var paths = new AppPaths();
        var bootstrap = new LoggerConfiguration();
        ConfigureLogging(bootstrap, paths);
        Log.Logger = bootstrap.CreateBootstrapLogger();
        InstallGlobalHandlers();

        try
        {
            CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(AppConfig.DefaultCulture);
            CultureInfo.CurrentUICulture = CultureInfo.DefaultThreadCurrentUICulture;
            var launchPaths = args.Select(a => Path.GetFullPath(a)).ToList();

            _host = BuildHost(paths);
            _logger = _host.Services.GetRequiredService<ILogger<App>>();

            var instance = _host.Services.GetRequiredService<ISingleInstanceService>();
            if (!instance.TryAcquire() && await instance.SendAsync(launchPaths, CancellationToken.None))
            {
                LogForwardedToFirstInstance(_logger);
                Shutdown();
                return;
            }

            await _host.StartAsync().ConfigureAwait(true);
            var preferences = _host.Services.GetRequiredService<IPreferencesService>();
            await preferences.LoadAsync(CancellationToken.None).ConfigureAwait(true);

            // Theme and accent before any window (rules/xaml.md).
            var theme = _host.Services.GetRequiredService<IAppThemeService>();
            theme.ApplyStartupTheme();
            theme.StartWatchingSystem();

            var window = _host.Services.GetRequiredService<MainWindow>();
            window.RestorePlacement(preferences.Current.WindowBounds, preferences.Current.WindowMaximized);
            MainWindow = window;
            window.Show();
            window.Activate();

            var shell = _host.Services.GetRequiredService<ShellViewModel>();
            instance.PathsReceived += (_, received) => OnPathsReceived(window, shell, received);
            instance.StartListening();
            LogStarted(_logger, AppConfig.AppName, AppConfig.AppVersion);

            await shell.InitializeAsync(window.ActualWidth, launchPaths, CancellationToken.None).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Startup cancelled: nothing to report.
        }
    }

    private void OnPathsReceived(Window window, ShellViewModel shell, IReadOnlyList<string> received)
    {
        // Background source: named pipe listener of the single instance service.
        Dispatcher.InvokeAsync(async () =>
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
            await shell.ReceivePathsAsync(received).ConfigureAwait(true);
        });
    }

    private void InstallGlobalHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Exception non geree hors du thread UI");
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "Tache en echec non observee");
            e.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exception non geree sur le thread UI");
        _host?.Services.GetService<IFeedbackService>()?.Show(new ResultError(FeedbackType.Danger, Strings.Error_Unexpected, e.Exception.Message));
        e.Handled = true;
    }
}
