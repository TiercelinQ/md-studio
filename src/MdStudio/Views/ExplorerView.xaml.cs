using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MdStudio.ViewModels;

namespace MdStudio.Views;

/// <summary>Explorer pane (zone D). Code-behind: forwards a click, Enter or Space on a row to the activate command.</summary>
public partial class ExplorerView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="ExplorerView"/> class.</summary>
    public ExplorerView()
    {
        InitializeComponent();
        Tree.PreviewMouseLeftButtonUp += (_, e) => Activate(RowOf(e.OriginalSource as DependencyObject));
        Tree.PreviewKeyDown += OnTreeKeyDown;
    }

    private static TreeViewItem? RowOf(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TreeViewItem item)
            {
                return item;
            }
        }

        return null;
    }

    private void OnTreeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Activate(RowOf(Keyboard.FocusedElement as DependencyObject));
            e.Handled = true;
        }
    }

    private void Activate(TreeViewItem? row)
    {
        if (row?.DataContext is FileNodeViewModel node && DataContext is ShellViewModel shell)
        {
            shell.Explorer.ActivateCommand.Execute(node);
        }
    }
}
