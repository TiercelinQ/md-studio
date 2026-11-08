using System.Globalization;
using System.Windows;
using System.Windows.Media;
using MdStudio.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace MdStudio.Services;

/// <summary>Applies the light or dark theme to the interface and exposes it to the document page.</summary>
public interface IAppThemeService
{
    /// <summary>Raised on the UI thread after the theme changed.</summary>
    event EventHandler? ThemeChanged;

    /// <summary>Gets the current theme.</summary>
    AppTheme Current { get; }

    /// <summary>Gets the current theme as CSS variables for the document page.</summary>
    PageTheme PageTheme { get; }

    /// <summary>Applies the persisted theme, or the Windows app theme when none was chosen.</summary>
    void ApplyStartupTheme();

    /// <summary>Switches between light and dark and remembers the manual choice.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when the choice is persisted.</returns>
    Task ToggleAsync(CancellationToken ct);

    /// <summary>Follows the Windows app theme while no manual choice was made (F-THE-04).</summary>
    void StartWatchingSystem();
}

/// <summary>WPF UI implementation of <see cref="IAppThemeService"/>.</summary>
/// <param name="preferences">Persisted settings.</param>
/// <param name="logger">Logger.</param>
public sealed partial class AppThemeService(IPreferencesService preferences, ILogger<AppThemeService> logger) : IAppThemeService, IDisposable
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Dictionnaire de jetons introuvable dans les ressources de l'application")]
    private static partial void LogTokenDictionaryMissing(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Jeton absent pour la page : {Resource}")]
    private static partial void LogPageTokenMissing(ILogger logger, string resource);

    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static readonly (string Variable, string Resource)[] PageTokens =
    [
        ("--chrome", "ChromeBrush"), ("--bar", "BarBrush"), ("--pane", "PaneBrush"), ("--doc", "DocBrush"),
        ("--fg", "FgBrush"), ("--fg2", "Fg2Brush"), ("--fg3", "Fg3Brush"), ("--stroke", "StrokeBrush"),
        ("--stroke2", "Stroke2Brush"), ("--hover", "HoverBrush"), ("--press", "PressBrush"),
        ("--accent", "AccentBrush"), ("--on-accent", "OnAccentBrush"), ("--accent-text", "AccentTextBrush"),
        ("--accent-soft", "AccentSoftBrush"), ("--code-bg", "CodeBgBrush"), ("--syn-k", "SynKBrush"),
        ("--syn-s", "SynSBrush"), ("--syn-c", "SynCBrush"), ("--mark", "MarkBrush"), ("--mark-cur", "MarkCurBrush"),
        ("--mark-fg", "MarkFgBrush"), ("--danger", "DangerBrush"),
    ];

    private bool _watching;

    /// <inheritdoc/>
    public event EventHandler? ThemeChanged;

    /// <inheritdoc/>
    public AppTheme Current { get; private set; } = AppTheme.Light;

    /// <inheritdoc/>
    public PageTheme PageTheme { get; private set; } = new(false, new Dictionary<string, string>());

    /// <inheritdoc/>
    public void ApplyStartupTheme() => Apply(preferences.Current.Theme ?? ReadSystemTheme());

    /// <inheritdoc/>
    public async Task ToggleAsync(CancellationToken ct)
    {
        var next = Current == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        Apply(next);
        await preferences.UpdateAsync(p => p with { Theme = next }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void StartWatchingSystem()
    {
        if (!_watching)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            _watching = true;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_watching)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
    }

    private static AppTheme ReadSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is 0 ? AppTheme.Dark : AppTheme.Light;
    }

    private static string ToCss(Color color) => string.Create(
        CultureInfo.InvariantCulture,
        $"rgba({color.R},{color.G},{color.B},{Math.Round(color.A / 255.0, 3)})");

    private void Apply(AppTheme theme)
    {
        // Accent first: the Fluent dictionary captures the accent when it loads (rules/xaml.md).
        var accent = (Color)ColorConverter.ConvertFromString(theme == AppTheme.Dark ? AppConfig.AccentColorDark : AppConfig.AccentColor);
        ApplicationAccentColorManager.Apply(accent, accent, accent, accent);
        ApplicationThemeManager.Apply(
            theme == AppTheme.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            WindowBackdropType.None,
            updateAccent: false);
        ReplaceTokenDictionary(theme);

        Current = theme;
        PageTheme = BuildPageTheme(theme);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceTokenDictionary(AppTheme theme)
    {
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            var source = dictionaries[i].Source?.OriginalString ?? string.Empty;
            if (source.EndsWith("Tokens.Light.xaml", StringComparison.Ordinal) || source.EndsWith("Tokens.Dark.xaml", StringComparison.Ordinal))
            {
                dictionaries[i] = new ResourceDictionary
                {
                    Source = new Uri($"pack://application:,,,/MdStudio;component/Themes/Tokens.{theme}.xaml", UriKind.Absolute),
                };
                return;
            }
        }

        LogTokenDictionaryMissing(logger);
    }

    private PageTheme BuildPageTheme(AppTheme theme)
    {
        var variables = new Dictionary<string, string>(PageTokens.Length);
        foreach (var (variable, resource) in PageTokens)
        {
            if (Application.Current.TryFindResource(resource) is SolidColorBrush brush)
            {
                variables[variable] = ToCss(brush.Color);
            }
            else
            {
                LogPageTokenMissing(logger, resource);
            }
        }

        return new PageTheme(theme == AppTheme.Dark, variables);
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General || preferences.Current.Theme is not null)
        {
            return;
        }

        var system = ReadSystemTheme();
        if (system != Current)
        {
            // Background source: SystemEvents raises on its own thread.
            Application.Current.Dispatcher.Invoke(() => Apply(system));
        }
    }
}
