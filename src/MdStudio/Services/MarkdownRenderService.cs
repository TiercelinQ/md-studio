using System.Globalization;
using System.IO;
using System.Text;
using Ganss.Xss;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdStudio.Models;

namespace MdStudio.Services;

/// <summary>Turns Markdown into sanitized HTML, one wrapper per top-level block.</summary>
public interface IMarkdownRenderService
{
    /// <summary>Renders a file off the UI thread.</summary>
    /// <param name="file">File to render; its folder resolves relative image paths.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered document and its source blocks.</returns>
    Task<RenderedDocument> RenderAsync(TextFile file, CancellationToken ct);
}

/// <summary>Markdig + HtmlSanitizer implementation of <see cref="IMarkdownRenderService"/>.</summary>
/// <param name="assets">Builds the page URL of local images.</param>
public sealed class MarkdownRenderService(IPageAssetProvider assets) : IMarkdownRenderService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePreciseSourceLocation()
        .UseYamlFrontMatter()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAutoLinks()
        .Build();

    /// <inheritdoc/>
    public Task<RenderedDocument> RenderAsync(TextFile file, CancellationToken ct)
    {
        var folder = Path.GetDirectoryName(file.Path) ?? string.Empty;
        return Task.Run(() => Render(file.Text, folder, ct), ct);
    }

    /// <summary>Renders Markdown synchronously.</summary>
    /// <param name="text">Markdown text.</param>
    /// <param name="folder">Folder used to resolve relative image paths.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rendered document.</returns>
    public RenderedDocument Render(string text, string folder, CancellationToken ct)
    {
        var document = Markdown.Parse(text, Pipeline);
        RewriteImages(document, folder);

        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);

        var html = new StringBuilder(text.Length * 2);
        var blocks = new List<SourceBlock>(document.Count);
        for (var i = 0; i < document.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var block = document[i];
            var nextStart = i + 1 < document.Count ? document[i + 1].Span.Start : text.Length;
            var start = Math.Clamp(block.Span.Start, 0, text.Length);
            var end = Math.Clamp(block.Span.End + 1, start, nextStart);

            // Some blocks (lists) end on their line break: it belongs to the separator, not to the block.
            while (end > start && text[end - 1] is '\r' or '\n')
            {
                end--;
            }

            blocks.Add(new SourceBlock(i, start, end - start, text[end..nextStart]));

            writer.GetStringBuilder().Clear();
            renderer.Render(block);
            writer.Flush();
            var inner = writer.ToString();
            var hidden = string.IsNullOrWhiteSpace(inner) ? " mdb-hidden" : string.Empty;
            html.Append(CultureInfo.InvariantCulture, $"<div class=\"mdb{hidden}\" data-src=\"{i}\">")
                .Append(inner)
                .Append("</div>\n");
        }

        var leading = blocks.Count > 0 ? text[..blocks[0].Start] : text;
        return new RenderedDocument(CreateSanitizer().Sanitize(html.ToString()), leading, blocks);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Add("input");
        foreach (var attribute in new[] { "type", "checked", "disabled", "class", "id", "start", "data-src", "data-md-src" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Add("mailto");
        return sanitizer;
    }

    private void RewriteImages(MarkdownDocument document, string folder)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!link.IsImage || string.IsNullOrWhiteSpace(link.Url))
            {
                continue;
            }

            if (Uri.TryCreate(link.Url, UriKind.Absolute, out var absolute) && absolute.Scheme != Uri.UriSchemeFile)
            {
                continue;
            }

            var local = absolute?.LocalPath ?? Path.Combine(folder, Uri.UnescapeDataString(link.Url));
            link.GetAttributes().AddProperty("data-md-src", link.Url);
            link.Url = assets.GetFileUrl(Path.GetFullPath(local));
        }
    }
}
