using System.Text.Json.Serialization;

namespace MdReader.Models;

/// <summary>Display mode of the window, shared by every tab.</summary>
public enum DocumentMode
{
    /// <summary>Read-only rendering.</summary>
    Reading,

    /// <summary>WYSIWYG editing.</summary>
    Editing,

    /// <summary>Raw Markdown text, read and edited as plain text.</summary>
    Source,
}

/// <summary>Theme sent to the document page: dark flag and CSS variables built from the color tokens.</summary>
/// <param name="IsDark">True for the dark theme (drives the Mermaid theme).</param>
/// <param name="Variables">CSS variable name (for example <c>--fg</c>) to CSS color.</param>
public sealed record PageTheme(bool IsDark, IReadOnlyDictionary<string, string> Variables);

/// <summary>Window-wide settings a document page needs when it loads.</summary>
/// <param name="Mode">Current mode.</param>
/// <param name="Zoom">Current zoom factor.</param>
/// <param name="Theme">Current theme.</param>
public sealed record DocumentPageState(DocumentMode Mode, double Zoom, PageTheme Theme);

/// <summary>Message sent by the application to the document page.</summary>
public sealed class HostMessage
{
    /// <summary>Gets the message type (<c>open</c>, <c>close</c>, <c>activate</c>, <c>load</c>, <c>setMode</c>, <c>showSource</c>, <c>setZoom</c>, <c>setTheme</c>, <c>format</c>, <c>search</c>, <c>searchStep</c>, <c>searchClear</c>, <c>scrollTo</c>, <c>serialize</c>, <c>getSource</c>, <c>focus</c>).</summary>
    public required string Type { get; init; }

    /// <summary>Gets the target tab identifier.</summary>
    public string? Tab { get; init; }

    /// <summary>Gets the document HTML (<c>load</c>).</summary>
    public string? Html { get; init; }

    /// <summary>Gets the Markdown text shown in source mode (<c>showSource</c>).</summary>
    public string? Source { get; init; }

    /// <summary>Gets the mode (<c>load</c>, <c>setMode</c>).</summary>
    public DocumentMode? Mode { get; init; }

    /// <summary>Gets the zoom factor (<c>load</c>, <c>setZoom</c>).</summary>
    public double? Zoom { get; init; }

    /// <summary>Gets the dark flag (<c>load</c>, <c>setTheme</c>).</summary>
    public bool? Dark { get; init; }

    /// <summary>Gets the CSS variables (<c>load</c>, <c>setTheme</c>).</summary>
    public IReadOnlyDictionary<string, string>? Variables { get; init; }

    /// <summary>Gets the formatting action (<c>format</c>).</summary>
    public FormatAction? Action { get; init; }

    /// <summary>Gets the first argument of the action: link address or image URL (<c>format</c>).</summary>
    public string? Argument { get; init; }

    /// <summary>Gets the second argument of the action: Markdown path of an image (<c>format</c>).</summary>
    public string? Extra { get; init; }

    /// <summary>Gets the searched text (<c>search</c>).</summary>
    public string? Query { get; init; }

    /// <summary>Gets the search direction, 1 or -1 (<c>searchStep</c>).</summary>
    public int? Direction { get; init; }

    /// <summary>Gets the heading anchor (<c>scrollTo</c>).</summary>
    public string? Anchor { get; init; }

    /// <summary>Gets the request identifier (<c>serialize</c>, <c>getSource</c>).</summary>
    public int? RequestId { get; init; }
}

/// <summary>Message sent by the document page to the application.</summary>
public sealed class PageMessage
{
    /// <summary>Gets the message type (<c>hostReady</c>, <c>ready</c>, <c>rendered</c>, <c>changed</c>, <c>sourceChanged</c>, <c>stats</c>, <c>currentHeading</c>, <c>searchResult</c>, <c>formatState</c>, <c>link</c>, <c>requestLink</c>, <c>serialized</c>, <c>zoomWheel</c>, <c>notify</c>, <c>drop</c>).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the source tab identifier.</summary>
    public string? Tab { get; init; }

    /// <summary>Gets the Markdown text of the source mode (<c>sourceChanged</c>, <c>serialized</c> of a <c>getSource</c>).</summary>
    public string? Source { get; init; }

    /// <summary>Gets the clicked link target (<c>link</c>).</summary>
    public string? Href { get; init; }

    /// <summary>Gets the edited blocks (<c>changed</c>, <c>serialized</c>).</summary>
    public IReadOnlyList<PageBlock>? Blocks { get; init; }

    /// <summary>Gets the outline (<c>changed</c>, <c>stats</c>).</summary>
    public IReadOnlyList<PageHeading>? Outline { get; init; }

    /// <summary>Gets the word count (<c>changed</c>, <c>stats</c>).</summary>
    public int? Words { get; init; }

    /// <summary>Gets the anchor of the current heading (<c>currentHeading</c>).</summary>
    public string? Heading { get; init; }

    /// <summary>Gets the current search result, 1-based (<c>searchResult</c>).</summary>
    public int? Current { get; init; }

    /// <summary>Gets the number of search results (<c>searchResult</c>).</summary>
    public int? Total { get; init; }

    /// <summary>Gets the formatting state at the caret (<c>formatState</c>).</summary>
    public PageFormatState? Format { get; init; }

    /// <summary>Gets the wheel direction, positive to zoom in (<c>zoomWheel</c>).</summary>
    public double? Delta { get; init; }

    /// <summary>Gets the notification key (<c>notify</c>).</summary>
    public string? Key { get; init; }

    /// <summary>Gets the request identifier (<c>serialized</c>).</summary>
    public int? RequestId { get; init; }
}

/// <summary>A block of the edited page as sent by the page.</summary>
public sealed class PageBlock
{
    /// <summary>Gets the index of an untouched source block.</summary>
    public int? Src { get; init; }

    /// <summary>Gets the Markdown of a modified or new block.</summary>
    public string? Md { get; init; }
}

/// <summary>A heading as sent by the page.</summary>
public sealed class PageHeading
{
    /// <summary>Gets the heading level, 1 to 3.</summary>
    public int Level { get; init; }

    /// <summary>Gets the heading text.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the heading identifier.</summary>
    public string Id { get; init; } = string.Empty;
}

/// <summary>Formatting at the caret, used to toggle the buttons of the formatting bar.</summary>
public sealed class PageFormatState
{
    /// <summary>Gets a value indicating whether the selection is bold.</summary>
    public bool Bold { get; init; }

    /// <summary>Gets a value indicating whether the selection is italic.</summary>
    public bool Italic { get; init; }

    /// <summary>Gets a value indicating whether the selection is struck through.</summary>
    public bool Strike { get; init; }

    /// <summary>Gets a value indicating whether the caret is in a bulleted list.</summary>
    public bool BulletList { get; init; }

    /// <summary>Gets a value indicating whether the caret is in a numbered list.</summary>
    public bool NumberedList { get; init; }

    /// <summary>Gets the current block style: <c>p</c>, <c>h1</c>, <c>h2</c>, <c>h3</c> or <c>blockquote</c>.</summary>
    public string Block { get; init; } = "p";
}

/// <summary>Source-generated JSON serialization of the page protocol.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(HostMessage))]
[JsonSerializable(typeof(PageMessage))]
public sealed partial class PageJsonContext : JsonSerializerContext;
