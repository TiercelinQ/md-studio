using System.IO;
using System.Reflection;
using System.Web;
using Microsoft.Extensions.Logging;

namespace MdStudio.Services;

/// <summary>A resource served to the document page.</summary>
/// <param name="Content">Content stream, owned by the caller.</param>
/// <param name="ContentType">MIME type.</param>
public sealed record PageAsset(Stream Content, string ContentType);

/// <summary>Serves the page files (embedded) and the images of the documents (disk) on the virtual host.</summary>
public interface IPageAssetProvider
{
    /// <summary>Gets the URL of the container page.</summary>
    string HostPageUrl { get; }

    /// <summary>Gets the URL of the page of one tab.</summary>
    /// <param name="tab">Tab identifier.</param>
    /// <returns>The URL.</returns>
    string GetDocumentPageUrl(string tab);

    /// <summary>Gets the URL under which a local file is served.</summary>
    /// <param name="fullPath">Absolute path.</param>
    /// <returns>The URL.</returns>
    string GetFileUrl(string fullPath);

    /// <summary>Resolves a request of the page.</summary>
    /// <param name="uri">Requested URI.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resource, or null when unknown or not allowed.</returns>
    Task<PageAsset?> ResolveAsync(Uri uri, CancellationToken ct);
}

/// <summary>Default <see cref="IPageAssetProvider"/>.</summary>
/// <param name="logger">Logger.</param>
public sealed partial class PageAssetProvider(ILogger<PageAssetProvider> logger) : IPageAssetProvider
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Ressource de page inconnue {Name}")]
    private static partial void LogUnknownPageResource(ILogger logger, string name);

    [LoggerMessage(Level = LogLevel.Information, Message = "Image indisponible {Path}")]
    private static partial void LogImageUnavailable(ILogger logger, Exception? exception, string path);

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".svg"] = "image/svg+xml",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
    };

    // MSBuild writes the sub-folder of a logical name with a backslash: normalize once to "web/vendor/x.js".
    private static readonly Dictionary<string, string> EmbeddedNames = Assembly.GetExecutingAssembly()
        .GetManifestResourceNames()
        .Where(n => n.StartsWith("web/", StringComparison.Ordinal))
        .ToDictionary(n => n.Replace('\\', '/'), n => n, StringComparer.Ordinal);

    private static string Origin => $"https://{AppConfig.VirtualHostName}";

    /// <inheritdoc/>
    public string HostPageUrl => $"{Origin}/app/host.html";

    /// <inheritdoc/>
    public string GetDocumentPageUrl(string tab) => $"{Origin}/app/doc.html?tab={Uri.EscapeDataString(tab)}";

    /// <inheritdoc/>
    public string GetFileUrl(string fullPath) => $"{Origin}/file?p={Uri.EscapeDataString(fullPath)}";

    /// <inheritdoc/>
    public Task<PageAsset?> ResolveAsync(Uri uri, CancellationToken ct)
    {
        if (!string.Equals(uri.Host, AppConfig.VirtualHostName, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<PageAsset?>(null);
        }

        var path = uri.AbsolutePath;
        if (path.StartsWith("/app/", StringComparison.Ordinal))
        {
            return Task.FromResult(Embedded(path["/app/".Length..]));
        }

        if (path == "/file")
        {
            return Task.Run(() => LocalImage(HttpUtility.ParseQueryString(uri.Query)["p"]), ct);
        }

        return Task.FromResult<PageAsset?>(null);
    }

    private static string? MimeOf(string path) => MimeTypes.GetValueOrDefault(Path.GetExtension(path));

    private PageAsset? Embedded(string name)
    {
        var mime = MimeOf(name);
        var stream = EmbeddedNames.TryGetValue($"web/{name}", out var resource)
            ? Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            : null;
        if (mime is null || stream is null)
        {
            stream?.Dispose();
            LogUnknownPageResource(logger, name);
            return null;
        }

        return new PageAsset(stream, mime);
    }

    private PageAsset? LocalImage(string? path)
    {
        // E-09: any absolute path is accepted, restricted to image extensions and read-only access.
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path)
            || !AppConfig.ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            return new PageAsset(stream, MimeOf(path) ?? "application/octet-stream");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogImageUnavailable(logger, ex, path);
            return null;
        }
    }
}
