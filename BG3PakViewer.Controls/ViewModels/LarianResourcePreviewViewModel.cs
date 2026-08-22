using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing Larian resource files (e.g…lsf/.lsx/.lsj/.lsb)
///     in file-explorer style: a breadcrumb bar on top showing the current path,
///     and a folder/file list below. Double-clicking a folder navigates into it;
///     clicking a breadcrumb jumps back to that level.
/// </summary>
public partial class LarianResourcePreviewViewModel : ObservableObject
{
    private readonly LarianResourceNodeViewModel _root;

    public LarianResourcePreviewViewModel(LarianResourceNodeViewModel root)
    {
        _root = root;
        Breadcrumbs.Add(root);
        RefreshCurrentItems();
    }

    /// <summary>
    ///     Breadcrumb trail from the root down to the current folder.
    /// </summary>
    public ObservableCollection<LarianResourceNodeViewModel> Breadcrumbs { get; } = [];

    /// <summary>
    ///     Contents of the current folder: folders first, then files.
    /// </summary>
    public ObservableCollection<LarianResourceNodeViewModel> CurrentItems { get; } = [];

    [ObservableProperty] public partial LarianResourceNodeViewModel? CurrentFolder { get; private set; }

    [ObservableProperty] public partial LarianResourceNodeViewModel? SelectedItem { get; private set; }

    [RelayCommand]
    private void SelectItem(LarianResourceNodeViewModel? item)
    {
        SelectedItem = item;
    }

    [RelayCommand]
    private void OpenFolder(LarianResourceNodeViewModel? folder)
    {
        if (folder == null || !folder.IsRegion || !folder.HasChildren) return;
        Breadcrumbs.Add(folder);
        CurrentFolder = folder;
        RefreshCurrentItems();
        SelectedItem = null;
    }

    [RelayCommand]
    private void NavigateTo(LarianResourceNodeViewModel? target)
    {
        if (target == null) return;

        // Find the target in the breadcrumb trail and truncate after it.
        var index = Breadcrumbs.IndexOf(target);
        if (index < 0) return;
        while (Breadcrumbs.Count > index + 1)
            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);

        CurrentFolder = target;
        RefreshCurrentItems();
        SelectedItem = null;
    }

    [RelayCommand]
    private void NavigateToRoot()
    {
        Breadcrumbs.Clear();
        Breadcrumbs.Add(_root);
        CurrentFolder = null;
        RefreshCurrentItems();
        SelectedItem = null;
    }

    private void RefreshCurrentItems()
    {
        CurrentItems.Clear();

        // At the root, show the root's children (the regions); otherwise show the
        // current folder's children.
        var children = CurrentFolder == null
            ? _root.Children
            : CurrentFolder.Children;

        foreach (var child in children.OrderByDescending(c => c.IsRegion).ThenBy(c => c.Name))
            CurrentItems.Add(child);
    }

    public static LarianResourcePreviewViewModel FromResource(Resource resource)
    {
        var root = new LarianResourceNodeViewModel("Root", true, null);
        foreach (var region in resource.Regions.Values)
            root.Children.Add(BuildNode(region, true));
        return new LarianResourcePreviewViewModel(root);
    }

    private static LarianResourceNodeViewModel BuildNode(Node node, bool isRegion)
    {
        var name = isRegion ? ((Region)node).RegionName : node.Name ?? string.Empty;

        var nodeViewModel = new LarianResourceNodeViewModel(name, isRegion, node);

        foreach (var child in node.Children.Values.SelectMany(childGroup => childGroup))
            nodeViewModel.Children.Add(BuildNode(child, false));

        return nodeViewModel;
    }
}
