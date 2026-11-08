using System.IO;

namespace MdStudio.Services;

/// <summary>Locations of the application data, all under <c>%APPDATA%\MdStudio</c>.</summary>
public interface IAppPaths
{
    /// <summary>Gets the user data folder.</summary>
    string UserData { get; }

    /// <summary>Gets the preferences file path.</summary>
    string PreferencesFile { get; }

    /// <summary>Gets the preferences file of the former application name, <c>%APPDATA%\MdReader</c>.</summary>
    string LegacyPreferencesFile { get; }

    /// <summary>Gets the log folder.</summary>
    string LogsFolder { get; }

    /// <summary>Gets the WebView2 user data folder.</summary>
    string WebViewData { get; }
}

/// <summary>Default <see cref="IAppPaths"/>, resolved once.</summary>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Initializes a new instance of the <see cref="AppPaths"/> class.</summary>
    public AppPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        UserData = Path.Combine(appData, AppConfig.AppName);
        PreferencesFile = Path.Combine(UserData, AppConfig.PreferencesFileName);
        LegacyPreferencesFile = Path.Combine(appData, AppConfig.LegacyAppName, AppConfig.PreferencesFileName);
        LogsFolder = Path.Combine(UserData, "logs");
        WebViewData = Path.Combine(UserData, "WebView2");
    }

    /// <inheritdoc/>
    public string UserData { get; }

    /// <inheritdoc/>
    public string PreferencesFile { get; }

    /// <inheritdoc/>
    public string LegacyPreferencesFile { get; }

    /// <inheritdoc/>
    public string LogsFolder { get; }

    /// <inheritdoc/>
    public string WebViewData { get; }
}
