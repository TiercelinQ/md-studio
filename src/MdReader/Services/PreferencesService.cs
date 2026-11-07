using System.IO;
using System.Text.Json;
using MdReader.Models;
using Microsoft.Extensions.Logging;

namespace MdReader.Services;

/// <summary>Loads and saves the persisted settings (<c>preferences.json</c>).</summary>
public interface IPreferencesService
{
    /// <summary>Gets the current settings.</summary>
    Preferences Current { get; }

    /// <summary>Loads the settings; defaults are used when the file is missing or unreadable.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when loaded.</returns>
    Task LoadAsync(CancellationToken ct);

    /// <summary>Changes the settings and writes them atomically.</summary>
    /// <param name="change">Function returning the new settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when written.</returns>
    Task UpdateAsync(Func<Preferences, Preferences> change, CancellationToken ct);
}

/// <summary>JSON implementation of <see cref="IPreferencesService"/>.</summary>
/// <param name="paths">Application paths.</param>
/// <param name="logger">Logger.</param>
public sealed partial class PreferencesService(IAppPaths paths, ILogger<PreferencesService> logger) : IPreferencesService, IDisposable
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Preferences illisibles, valeurs par defaut utilisees")]
    private static partial void LogPreferencesUnreadable(ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Echec d'ecriture des preferences")]
    private static partial void LogPreferencesWriteFailed(ILogger logger, Exception? exception);

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc/>
    public Preferences Current { get; private set; } = new();

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(paths.PreferencesFile))
            {
                return;
            }

            await using var stream = File.OpenRead(paths.PreferencesFile);
            var loaded = await JsonSerializer.DeserializeAsync(stream, PreferencesJsonContext.Default.Preferences, ct).ConfigureAwait(false);
            Current = Sanitize(loaded ?? new Preferences());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LogPreferencesUnreadable(logger, ex);
            Current = new Preferences();
        }
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(Func<Preferences, Preferences> change, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Current = Sanitize(change(Current));
            Directory.CreateDirectory(paths.UserData);
            var temp = paths.PreferencesFile + ".tmp";
            await using (var stream = File.Create(temp))
            {
                await JsonSerializer.SerializeAsync(stream, Current, PreferencesJsonContext.Default.Preferences, ct).ConfigureAwait(false);
            }

            File.Move(temp, paths.PreferencesFile, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPreferencesWriteFailed(logger, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private static Preferences Sanitize(Preferences value) => value with
    {
        ExplorerWidth = Math.Clamp(value.ExplorerWidth, AppConfig.ExplorerWidthMin, AppConfig.ExplorerWidthMax),
        Zoom = Math.Round(Math.Clamp(value.Zoom, AppConfig.ZoomMin, AppConfig.ZoomMax), 1),
        SnackbarPosition = AppConfig.SnackbarPosition,
    };
}
