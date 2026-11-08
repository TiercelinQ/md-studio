using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MdStudio.Models;

namespace MdStudio.ViewModels;

/// <summary>A row of the explorer tree.</summary>
public sealed partial class FileNodeViewModel : ObservableObject
{
    /// <summary>Initializes a new instance of the <see cref="FileNodeViewModel"/> class.</summary>
    /// <param name="node">Folder or file.</param>
    /// <param name="depth">Depth, 0 for the first level.</param>
    /// <param name="expandedFolders">Folders to show expanded.</param>
    public FileNodeViewModel(FileNode node, int depth, IReadOnlySet<string> expandedFolders)
    {
        Name = node.Name;
        FullPath = node.FullPath;
        IsFolder = node.IsFolder;
        Depth = depth;
        IsExpanded = node.IsFolder && expandedFolders.Contains(node.FullPath);
        Children = new ObservableCollection<FileNodeViewModel>(
            node.Children.Select(c => new FileNodeViewModel(c, depth + 1, expandedFolders)));
    }

    /// <summary>Gets the displayed name.</summary>
    public string Name { get; }

    /// <summary>Gets the absolute path.</summary>
    public string FullPath { get; }

    /// <summary>Gets a value indicating whether the row is a folder.</summary>
    public bool IsFolder { get; }

    /// <summary>Gets the depth, used for the indentation.</summary>
    public int Depth { get; }

    /// <summary>Gets the children.</summary>
    public ObservableCollection<FileNodeViewModel> Children { get; }

    /// <summary>Gets or sets a value indicating whether the user expanded the folder (kept while filtering).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial bool IsExpanded { get; set; }

    /// <summary>Gets or sets a value indicating whether a filter is active (folders then shown expanded).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOpen))]
    public partial bool IsFiltering { get; set; }

    /// <summary>Gets or sets a value indicating whether the row matches the filter.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the row is the active document or the keyboard selection.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets or sets a value indicating whether the folder is shown expanded.</summary>
    public bool IsOpen
    {
        get => IsFolder && (IsFiltering || IsExpanded);
        set
        {
            if (IsFolder && !IsFiltering)
            {
                IsExpanded = value;
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Enumerates this row and all its descendants.</summary>
    /// <returns>The rows, depth first.</returns>
    public IEnumerable<FileNodeViewModel> SelfAndDescendants()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(c => c.SelfAndDescendants()))
        {
            yield return descendant;
        }
    }

    /// <summary>Applies a filter on file names; folders stay visible when a descendant matches.</summary>
    /// <param name="filter">Text to find, or null for no filter.</param>
    /// <returns>True when the row is visible.</returns>
    public bool ApplyFilter(string? filter)
    {
        IsFiltering = filter is not null;
        if (IsFolder)
        {
            var any = false;
            foreach (var child in Children)
            {
                any |= child.ApplyFilter(filter);
            }

            IsVisible = filter is null || any;
        }
        else
        {
            IsVisible = filter is null || Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
        }

        return IsVisible;
    }
}
