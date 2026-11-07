using System.ComponentModel;
using System.Windows.Controls;
using MdReader.ViewModels;

namespace MdReader.Views;

/// <summary>Document area (zone F). Code-behind: gives the focus to the search field when asked.</summary>
public partial class DocumentView : UserControl
{
    private ShellViewModel? _shell;
    private DocumentViewModel? _document;

    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachShell(DataContext as ShellViewModel);
    }

    private void AttachShell(ShellViewModel? shell)
    {
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
        }

        _shell = shell;
        if (_shell is not null)
        {
            _shell.PropertyChanged += OnShellPropertyChanged;
        }

        AttachDocument(_shell?.ActiveDocument);
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.ActiveDocument))
        {
            AttachDocument(_shell?.ActiveDocument);
        }
    }

    private void AttachDocument(DocumentViewModel? document)
    {
        if (_document is not null)
        {
            _document.SearchFocusRequested -= OnSearchFocusRequested;
        }

        _document = document;
        if (_document is not null)
        {
            _document.SearchFocusRequested += OnSearchFocusRequested;
        }
    }

    private void OnSearchFocusRequested(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        });
    }
}
