using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing Larian resource files (e.g…lsf/.lsx/.lsj/.lsb).
///     Renders the resource structure directly as a tree of regions/nodes; a node's
///     attributes are shown in a detail panel when the node is selected. No string
///     (LSX) serialization is performed for previewing.
/// </summary>
public partial class LarianResourcePreviewViewModel : ObservableObject
{
    public ObservableCollection<LarianResourceNodeViewModel> RootNodes { get; } = [];

    [ObservableProperty] public partial LarianResourceNodeViewModel? SelectedNode { get; private set; }

    [RelayCommand]
    private void SelectNode(LarianResourceNodeViewModel? node)
    {
        SelectedNode = node;
    }

    public static LarianResourcePreviewViewModel FromResource(Resource resource)
    {
        var viewModel = new LarianResourcePreviewViewModel();

        // Top-level root wrappers, mirroring the main file tree.
        var root = new LarianResourceNodeViewModel("Root", true, null);
        viewModel.RootNodes.Add(root);

        foreach (var region in resource.Regions.Values)
            root.Children.Add(BuildNode(region, true));

        viewModel.SelectedNode = viewModel.FindFirstNode();
        return viewModel;
    }

    private LarianResourceNodeViewModel? FindFirstNode()
    {
        var first = RootNodes.FirstOrDefault();
        return first is { HasChildren: true } ? FindFirstNode(first) : first;
    }

    private static LarianResourceNodeViewModel FindFirstNode(LarianResourceNodeViewModel node)
    {
        return node.Children.Count > 0 ? FindFirstNode(node.Children[0]) : node;
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