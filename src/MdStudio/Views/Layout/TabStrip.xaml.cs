using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MdStudio.ViewModels;

namespace MdStudio.Views.Layout;

/// <summary>Document tabs. Code-behind: middle click to close and keeping the active tab in view.</summary>
public partial class TabStrip : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="TabStrip"/> class.</summary>
    public TabStrip()
    {
        InitializeComponent();
        DocumentTabs.PreviewMouseDown += OnPreviewMouseDown;
        DocumentTabs.SelectionChanged += (_, _) => BringActiveIntoView();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || DataContext is not ShellViewModel shell)
        {
            return;
        }

        for (var element = e.OriginalSource as DependencyObject; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TabItem { DataContext: DocumentViewModel document })
            {
                shell.CloseTabCommand.Execute(document);
                e.Handled = true;
                return;
            }
        }
    }

    private void BringActiveIntoView()
    {
        if (DocumentTabs.ItemContainerGenerator.ContainerFromItem(DocumentTabs.SelectedItem) is TabItem tab)
        {
            tab.BringIntoView();
        }
    }
}
