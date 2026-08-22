using System.Collections.ObjectModel;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     A single structural node in the Larian resource preview tree.
///     Represents a region or a node; its attributes are lazily loaded from the
///     underlying <see cref="Node"/> only when the node is selected, so building the
///     tree for large resources does not pay the cost of formatting every attribute.
/// </summary>
public class LarianResourceNodeViewModel(string name, bool isRegion, Node? source)
{
    public string Name { get; } = name;

    public bool IsRegion { get; } = isRegion;

    public bool HasChildren => Children.Count > 0;

    public ObservableCollection<LarianResourceNodeViewModel> Children { get; } = [];

    /// <summary>
    ///     Attributes owned by this node, built on first access.
    /// </summary>
    public ObservableCollection<LarianAttributeViewModel> Attributes =>
        field ??= BuildAttributes();

    private ObservableCollection<LarianAttributeViewModel> BuildAttributes()
    {
        var attributes = new ObservableCollection<LarianAttributeViewModel>();
        if (source?.Attributes is not { } sourceAttributes) return attributes;

        foreach (var (key, attribute) in sourceAttributes)
        {
            attributes.Add(new LarianAttributeViewModel
            {
                Key = key,
                Value = FormatAttributeValue(attribute)
            });
        }

        return attributes;
    }

    private static string FormatAttributeValue(NodeAttribute attribute)
    {
        try
        {
            return attribute.AsString(new NodeSerializationSettings());
        }
        catch (Exception)
        {
            return attribute.Value?.ToString() ?? string.Empty;
        }
    }
}

/// <summary>
///     A single attribute rendered in the selected node's detail panel.
/// </summary>
public class LarianAttributeViewModel
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public string DisplayText => $"{Key}: {Value}";
}
