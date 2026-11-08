using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using MdStudio.Models;
using MdStudio.Resources;
using Microsoft.Extensions.Logging;

namespace MdStudio.Services;

/// <summary>Opens web links of a document in the default browser (F-LEC-04).</summary>
public interface IExternalLauncher
{
    /// <summary>Opens an absolute <c>http</c> or <c>https</c> address; anything else is refused.</summary>
    /// <param name="url">Address found in the document.</param>
    /// <returns>Success, or a failure when refused or when no browser could start.</returns>
    Result<Unit> Open(string url);
}

/// <summary>Shell implementation of <see cref="IExternalLauncher"/> (deviation E-08).</summary>
/// <param name="logger">Logger.</param>
public sealed partial class ExternalLauncher(ILogger<ExternalLauncher> logger) : IExternalLauncher
{
    private static readonly CompositeFormat LinkRefusedFormat = CompositeFormat.Parse(Strings.Toast_LinkRefused_Message);
    private static readonly CompositeFormat NoBrowserFormat = CompositeFormat.Parse(Strings.Toast_NoBrowser_Message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lien refuse {Url}")]
    private static partial void LogLinkRefused(ILogger logger, string url);

    [LoggerMessage(Level = LogLevel.Error, Message = "Navigateur introuvable pour {Url}")]
    private static partial void LogBrowserMissing(ILogger logger, Exception? exception, string url);

    /// <summary>Checks that an address is an absolute <c>http</c> or <c>https</c> URI.</summary>
    /// <param name="url">Address to check.</param>
    /// <param name="uri">The parsed URI when allowed.</param>
    /// <returns>True when allowed.</returns>
    public static bool IsAllowed(string url, out Uri? uri)
    {
        uri = Uri.TryCreate(url, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            ? parsed
            : null;
        return uri is not null;
    }

    /// <inheritdoc/>
    public Result<Unit> Open(string url)
    {
        if (!IsAllowed(url, out var uri) || uri is null)
        {
            LogLinkRefused(logger, url);
            return Result<Unit>.Failure(new ResultError(
                FeedbackType.Danger, Strings.Toast_LinkNotOpened_Title, string.Format(CultureInfo.CurrentCulture, LinkRefusedFormat, url)));
        }

        try
        {
            // E-08: shell execution limited to a validated http(s) URI, never a program path.
            using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return Result<Unit>.Success(Unit.Value);
        }
        catch (Win32Exception ex)
        {
            LogBrowserMissing(logger, ex, url);
            return Result<Unit>.Failure(new ResultError(
                FeedbackType.Danger, Strings.Toast_LinkNotOpened_Title, string.Format(CultureInfo.CurrentCulture, NoBrowserFormat, url)));
        }
    }
}
