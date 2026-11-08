using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MdStudio.Models;
using MdStudio.ViewModels;

namespace MdStudio.Views;

/// <summary>Outline pane (zone G). Code-behind: forwards a click or Enter on an entry to the navigate command.</summary>
public partial class OutlineView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="OutlineView"/> class.</summary>
    public OutlineView()
    {
        InitializeComponent();
        Entries.PreviewMouseLeftButtonUp += (_, e) => Navigate(EntryOf(e.OriginalSource as DependencyObject));
        Entries.PreviewKeyDown += OnEntriesKeyDown;
    }

    private static OutlineEntry? EntryOf(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is ListBoxItem { DataContext: OutlineEntry entry })
            {
                return entry;
            }
        }

        return null;
    }

    private void OnEntriesKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            Navigate(EntryOf(Keyboard.FocusedElement as DependencyObject));
            e.Handled = true;
        }
    }

    private void Navigate(OutlineEntry? entry)
    {
        if (entry is not null && DataContext is ShellViewModel { ActiveDocument: { } document })
        {
            document.NavigateOutlineCommand.Execute(entry);
        }
    }
}
