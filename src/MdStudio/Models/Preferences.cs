using System.Text.Json.Serialization;

namespace MdStudio.Models;

/// <summary>Application theme.</summary>
public enum AppTheme
{
    /// <summary>Light theme.</summary>
    Light,

    /// <summary>Dark theme.</summary>
    Dark,
}

/// <summary>Window position and size, in device independent pixels.</summary>
/// <param name="Left">Left edge.</param>
/// <param name="Top">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public sealed record WindowBounds(double Left, double Top, double Width, double Height);

/// <summary>Settings persisted between launches (specification 4.18).</summary>
public sealed record Preferences
{
    /// <summary>Gets or sets the last opened folder, or null.</summary>
    public string? LastFolder { get; set; }

    /// <summary>Gets or sets the explorer visibility chosen by the user, or null to use the window width rule.</summary>
    public bool? ExplorerVisible { get; set; }

    /// <summary>Gets or sets the outline visibility chosen by the user, or null to use the window width rule.</summary>
    public bool? OutlineVisible { get; set; }

    /// <summary>Gets or sets the explorer width.</summary>
    public double ExplorerWidth { get; set; } = AppConfig.ExplorerWidthDefault;

    /// <summary>Gets or sets the document zoom factor.</summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>Gets or sets the manually chosen theme, or null to follow Windows.</summary>
    public AppTheme? Theme { get; set; }

    /// <summary>Gets or sets the last window bounds, or null for the default centered window.</summary>
    public WindowBounds? WindowBounds { get; set; }

    /// <summary>Gets or sets a value indicating whether the window was maximized.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>Gets or sets the notification anchor (specification layout 5).</summary>
    public string SnackbarPosition { get; set; } = AppConfig.SnackbarPosition;
}

/// <summary>Source-generated JSON serialization of the preferences.</summary>
// Settable properties: with init-only ones the generated serializer resets missing values to 0 instead of their defaults.
[JsonSourceGenerationOptions(
    WriteIndented = true,
    UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(Preferences))]
public sealed partial class PreferencesJsonContext : JsonSerializerContext;
