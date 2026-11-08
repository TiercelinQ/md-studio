using MdReader.Models;

namespace MdReader.ViewModels;

/// <summary>
/// Page of one tab, implemented by the view hosting WebView2 (deviation E-10). The ViewModel drives
/// the page through this contract only; tests substitute it.
/// </summary>
public interface IDocumentSurface
{
    /// <summary>Loads a rendered document once the page is ready.</summary>
    /// <param name="html">Sanitized HTML.</param>
    /// <param name="state">Mode, zoom and theme to apply.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task completed when the load message is sent.</returns>
    Task LoadAsync(string html, DocumentPageState state, CancellationToken ct);

    /// <summary>Switches the display mode; entering the source mode goes through <see cref="ShowSource"/>.</summary>
    /// <param name="mode">New mode.</param>
    void SetMode(DocumentMode mode);

    /// <summary>Shows the raw Markdown text, editable, in place of the rendering (source mode).</summary>
    /// <param name="text">Markdown text.</param>
    void ShowSource(string text);

    /// <summary>Sets the zoom factor of the document text.</summary>
    /// <param name="zoom">Factor, 0.7 to 2.0.</param>
    void SetZoom(double zoom);

    /// <summary>Applies a theme to the page.</summary>
    /// <param name="theme">Theme variables.</param>
    void SetTheme(PageTheme theme);

    /// <summary>Applies a formatting command at the saved selection.</summary>
    /// <param name="action">Command.</param>
    /// <param name="argument">Link address or image URL.</param>
    /// <param name="extra">Markdown path of an image.</param>
    void ApplyFormat(FormatAction action, string? argument = null, string? extra = null);

    /// <summary>Highlights the occurrences of a text and selects the first one.</summary>
    /// <param name="query">Trimmed text, at least 2 characters.</param>
    void Search(string query);

    /// <summary>Moves to the next or previous occurrence, circularly.</summary>
    /// <param name="direction">1 for next, -1 for previous.</param>
    void SearchStep(int direction);

    /// <summary>Removes the search highlighting.</summary>
    void ClearSearch();

    /// <summary>Scrolls a heading to the top of the document.</summary>
    /// <param name="anchor">Heading identifier.</param>
    void ScrollTo(string anchor);

    /// <summary>Moves the keyboard focus into the document.</summary>
    void Focus();

    /// <summary>Returns the blocks of the page, untouched ones by source index, others as Markdown.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The blocks in page order.</returns>
    Task<IReadOnlyList<EditedBlock>> SerializeAsync(CancellationToken ct);

    /// <summary>Returns the Markdown text of the source mode, as typed (line feeds only).</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The text.</returns>
    Task<string> GetSourceAsync(CancellationToken ct);
}
