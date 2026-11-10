namespace MdStudio;

/// <summary>Application constants shared by several files.</summary>
public static class AppConfig
{
    /// <summary>Application name, also the data folder name under <c>%APPDATA%</c>.</summary>
    public const string AppName = "MdStudio";

    /// <summary>Former name of the application: its preferences are taken over once, on first launch.</summary>
    public const string LegacyAppName = "MdReader";

    /// <summary>Application version, mirror of the project <c>Version</c> (rules/versioning.md).</summary>
    public const string AppVersion = "1.2.1";

    /// <summary>Accent of the light theme (design system, specification 5.2).</summary>
    public const string AccentColor = "#0067C0";

    /// <summary>Accent of the dark theme (specification 5.2, deviation E-15).</summary>
    public const string AccentColorDark = "#60CDFF";

    /// <summary>Preferences file name.</summary>
    public const string PreferencesFileName = "preferences.json";

    /// <summary>Interface culture.</summary>
    public const string DefaultCulture = "fr";

    /// <summary>Virtual host serving the document page (never resolved on the network).</summary>
    public const string VirtualHostName = "mdstudio.local";

    /// <summary>Minimum window width (specification T-09).</summary>
    public const double WindowMinWidth = 360;

    /// <summary>Minimum window height (specification T-09).</summary>
    public const double WindowMinHeight = 520;

    /// <summary>Default window width.</summary>
    public const double WindowDefaultWidth = 1280;

    /// <summary>Default window height.</summary>
    public const double WindowDefaultHeight = 800;

    /// <summary>At or below this window width, the panes become overlays (specification 3.6, deviation E-12).</summary>
    public const double NarrowWidthThreshold = 820;

    /// <summary>At or below this window width, the command bar wraps (specification 3.6, deviation E-12).</summary>
    public const double CompactWidthThreshold = 560;

    /// <summary>Minimum explorer width (deviation E-15).</summary>
    public const double ExplorerWidthMin = 180;

    /// <summary>Maximum explorer width (deviation E-15).</summary>
    public const double ExplorerWidthMax = 420;

    /// <summary>Default explorer width (deviation E-15).</summary>
    public const double ExplorerWidthDefault = 264;

    /// <summary>Minimum zoom factor.</summary>
    public const double ZoomMin = 0.7;

    /// <summary>Maximum zoom factor.</summary>
    public const double ZoomMax = 2.0;

    /// <summary>Zoom step.</summary>
    public const double ZoomStep = 0.1;

    /// <summary>Above this size a confirmation is asked before opening (specification 4.17).</summary>
    public const long LargeFileThresholdBytes = 5L * 1024 * 1024;

    /// <summary>Display time of a success toast (toast specification, section 2).</summary>
    public const int ToastSuccessMs = 4500;

    /// <summary>Display time of an information toast.</summary>
    public const int ToastInfoMs = 6000;

    /// <summary>Display time of a warning toast. Errors stay until closed.</summary>
    public const int ToastWarningMs = 9000;

    /// <summary>Maximum number of toasts shown at once (TS-13).</summary>
    public const int ToastMaxVisible = 4;

    /// <summary>A message repeated within this time restarts its toast instead of stacking (TS-49).</summary>
    public const int ToastRepeatWindowMs = 2000;

    /// <summary>Delay between the close of a toast and its removal, covering its exit animation (section 5.6).</summary>
    public const int ToastRemoveDelayMs = 200;

    /// <summary>Notification anchor (layout.md 5): bottom right, as the toast specification sets (TS-10).</summary>
    public const string SnackbarPosition = "bottom-right";

    /// <summary>Minimum log level.</summary>
    public const string LogLevel = "Information";

    /// <summary>Size limit of a log file.</summary>
    public const long LogMaxBytes = 1_000_000;

    /// <summary>Gets the opened file extensions (decision D6).</summary>
    public static IReadOnlyList<string> MarkdownExtensions { get; } = [".md", ".markdown"];

    /// <summary>Gets the image extensions served to the page.</summary>
    public static IReadOnlyList<string> ImageExtensions { get; } = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".bmp"];
}
