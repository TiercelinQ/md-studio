using System.Windows.Controls;

namespace MdReader.Views.Dialogs;

/// <summary>Content of the link dialog. Code-behind: puts the caret at the end of the address field.</summary>
public partial class LinkDialogContent : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="LinkDialogContent"/> class.</summary>
    public LinkDialogContent()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UrlBox.Focus();
            UrlBox.CaretIndex = UrlBox.Text.Length;
        };
    }
}
