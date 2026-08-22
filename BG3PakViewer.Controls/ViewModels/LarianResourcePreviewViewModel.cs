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
        var root = new LarianResourceNodeViewModel("Root", null);

        // Show the resource metadata (version/build) when the root is selected.
        root.AddAttribute("MajorVersion", resource.Metadata.MajorVersion.ToString())
            .AddAttribute("MinorVersion", resource.Metadata.MinorVersion.ToString())
            .AddAttribute("Revision", resource.Metadata.Revision.ToString())
            .AddAttribute("BuildNumber", resource.Metadata.BuildNumber.ToString())
            .AddAttribute("Timestamp", resource.Metadata.Timestamp.ToString());

        viewModel.RootNodes.Add(root);
        foreach (var region in resource.Regions.Values)
            root.Children.Add(BuildNode(region, true));

        // Select the root by default so the metadata is shown on open.
        viewModel.SelectedNode = root;
        return viewModel;
    }

    private static LarianResourceNodeViewModel BuildNode(Node node, bool isRegion)
    {
        var name = isRegion ? ((Region)node).RegionName : node.Name ?? string.Empty;

        var nodeViewModel = new LarianResourceNodeViewModel(name, node);

        foreach (var child in node.Children.Values.SelectMany(childGroup => childGroup))
            nodeViewModel.Children.Add(BuildNode(child, false));

        return nodeViewModel;
    }
}