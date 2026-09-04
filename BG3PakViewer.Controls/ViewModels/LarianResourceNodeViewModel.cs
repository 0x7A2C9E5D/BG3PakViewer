using System.Collections.ObjectModel;
using LSLib.LS;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     A single structural node in the Larian resource preview tree.
///     Represents a region or a node; its attributes are lazily loaded from the
///     underlying <see cref="Node" /> only when the node is selected, so building the
///     tree for large resources does not pay the cost of formatting every attribute.
/// </summary>
public class LarianResourceNodeViewModel(string name, Node? source)
{
    /// <summary>
    ///     The name of the node.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///     The source node, if any.
    /// </summary>
    public ObservableCollection<LarianResourceNodeViewModel> Children { get; } = [];

    /// <summary>
    ///     Attributes owned by this node, built on first access.
    /// </summary>
    public ObservableCollection<LarianAttributeViewModel> Attributes =>
        field ??= BuildAttributes();

    /// <summary>
    ///     Adds an explicit attribute (used for synthetic nodes such as the resource
    ///     version header that have no underlying <see cref="Node" />).
    /// </summary>
    public LarianResourceNodeViewModel AddAttribute(string key, string value)
    {
        Attributes.Add(new LarianAttributeViewModel { Key = key, Value = value });
        return this;
    }

    /// <summary>
    ///     Builds the attributes for this node.
    /// </summary>
    /// <returns></returns>
    private ObservableCollection<LarianAttributeViewModel> BuildAttributes()
    {
        var attributes = new ObservableCollection<LarianAttributeViewModel>();
        if (source?.Attributes is not { } sourceAttributes) return attributes;
        foreach (var (key, attribute) in sourceAttributes)
            attributes.Add(new LarianAttributeViewModel
            {
                Key = key,
                Value = FormatAttributeValue(attribute)
            });
        return attributes;
    }

    /// <summary>
    ///     Formats the value of an attribute.
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
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
    /// <summary>
    ///     The attribute key.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    ///     The attribute value.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    ///     The display text for the attribute.
    /// </summary>
    public string DisplayText => $"{Key}: {Value}";
}