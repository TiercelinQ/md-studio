using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Data;
using MdReader.Resources;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace MdReader.Services;

/// <summary>Answer of the "save changes" dialog.</summary>
public enum SaveDecision
{
    /// <summary>Save, then continue.</summary>
    Save,

    /// <summary>Continue without saving.</summary>
    Discard,

    /// <summary>Stop the operation.</summary>
    Cancel,
}

/// <summary>Windows pickers and application dialogs (component C-19). UI adapter, deviation E-11.</summary>
public interface IDialogService
{
    /// <summary>Asks for a Markdown file.</summary>
    /// <returns>The chosen path, or null.</returns>
    string? PickMarkdownFile();

    /// <summary>Asks for the name and folder of a new Markdown file. Does not ask to overwrite.</summary>
    /// <param name="initialFolder">Folder shown first, or null for the default one.</param>
    /// <returns>The chosen path (extension <c>.md</c> added when missing), or null.</returns>
    string? PickNewMarkdownFile(string? initialFolder);

    /// <summary>Asks for a folder.</summary>
    /// <returns>The chosen path, or null.</returns>
    string? PickFolder();

    /// <summary>Asks for an image file.</summary>
    /// <param name="initialFolder">Folder shown first.</param>
    /// <returns>The chosen path, or null.</returns>
    string? PickImage(string initialFolder);

    /// <summary>Asks whether to save a modified document.</summary>
    /// <param name="fileName">Name of the document.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The decision; Escape means <see cref="SaveDecision.Cancel"/>.</returns>
    Task<SaveDecision> ConfirmSaveAsync(string fileName, CancellationToken ct);

    /// <summary>Asks whether to open a large file.</summary>
    /// <param name="fileName">Name of the file.</param>
    /// <param name="megabytes">Size in megabytes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True to open it.</returns>
    Task<bool> ConfirmLargeFileAsync(string fileName, double megabytes, CancellationToken ct);

    /// <summary>
    /// Shows a dialog around a content object, rendered through its data template. The primary
    /// button is bound to the <c>CanConfirm</c> property of the content when it exists.
    /// </summary>
    /// <param name="title">Dialog title.</param>
    /// <param name="content">Content object (a ViewModel).</param>
    /// <param name="primaryText">Primary button text.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when the primary button was used.</returns>
    Task<bool> ShowContentAsync(string title, object content, string primaryText, CancellationToken ct);
}

/// <summary>WPF and WPF UI implementation of <see cref="IDialogService"/>.</summary>
/// <param name="dialogs">WPF UI dialog service holding the dialog host of the shell.</param>
public sealed class DialogService(IContentDialogService dialogs) : IDialogService
{
    private static readonly CompositeFormat SaveTextFormat = CompositeFormat.Parse(Strings.Dialog_Save_Text);
    private static readonly CompositeFormat LargeTextFormat = CompositeFormat.Parse(Strings.Dialog_Large_Text);

    /// <inheritdoc/>
    public string? PickMarkdownFile()
    {
        var dialog = new OpenFileDialog { Filter = Strings.Filter_Markdown, CheckFileExists = true };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? PickNewMarkdownFile(string? initialFolder)
    {
        var dialog = new SaveFileDialog
        {
            Filter = Strings.Filter_Markdown,
            DefaultExt = ".md",
            AddExtension = true,
            OverwritePrompt = false,
            FileName = Strings.NewFile_DefaultName,
            InitialDirectory = initialFolder ?? string.Empty,
        };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog();
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FolderName : null;
    }

    /// <inheritdoc/>
    public string? PickImage(string initialFolder)
    {
        var dialog = new OpenFileDialog { Filter = Strings.Filter_Image, CheckFileExists = true, InitialDirectory = initialFolder };
        return dialog.ShowDialog(Application.Current.MainWindow) == true ? dialog.FileName : null;
    }

    /// <inheritdoc/>
    public async Task<SaveDecision> ConfirmSaveAsync(string fileName, CancellationToken ct)
    {
        var dialog = Create(Strings.Dialog_Save_Title, string.Format(CultureInfo.CurrentCulture, SaveTextFormat, fileName));
        dialog.PrimaryButtonText = Strings.Dialog_Save_Primary;
        dialog.SecondaryButtonText = Strings.Dialog_Save_Secondary;
        var result = await dialogs.ShowAsync(dialog, ct);
        return result switch
        {
            ContentDialogResult.Primary => SaveDecision.Save,
            ContentDialogResult.Secondary => SaveDecision.Discard,
            _ => SaveDecision.Cancel,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmLargeFileAsync(string fileName, double megabytes, CancellationToken ct)
    {
        var dialog = Create(Strings.Dialog_Large_Title, string.Format(CultureInfo.CurrentCulture, LargeTextFormat, fileName, megabytes));
        dialog.PrimaryButtonText = Strings.Dialog_Large_Primary;
        return await dialogs.ShowAsync(dialog, ct) == ContentDialogResult.Primary;
    }

    /// <inheritdoc/>
    public async Task<bool> ShowContentAsync(string title, object content, string primaryText, CancellationToken ct)
    {
        var dialog = Create(title, content);
        dialog.PrimaryButtonText = primaryText;
        if (content.GetType().GetProperty("CanConfirm") is not null)
        {
            dialog.SetBinding(ContentDialog.IsPrimaryButtonEnabledProperty, new Binding("CanConfirm") { Source = content });
        }

        return await dialogs.ShowAsync(dialog, ct) == ContentDialogResult.Primary;
    }

    private static ContentDialog Create(string title, object content) => new()
    {
        Title = title,
        Content = content,
        CloseButtonText = Strings.Dialog_Cancel,
        DefaultButton = ContentDialogButton.Primary,
        Style = Application.Current.TryFindResource("DialogStyle") as Style,
    };
}
