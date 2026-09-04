using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace BG3PakViewer.Shared.Models;

/// <summary>
///     PackageEntry
/// </summary>
public class PackageEntry
{
    /// <summary>
    ///     Gets or sets the name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets or sets the full path.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    ///     Gets or sets a value indicating whether this <see cref="PackageEntry" /> is folder.
    /// </summary>
    public bool IsFolder { get; set; }

    /// <summary>
    ///     Gets or sets the children.
    /// </summary>
    public ObservableCollection<PackageEntry> Children { get; } = [];

    /// <summary>
    ///     Gets the file extension.
    /// </summary>
    public string FileExtension => Path.GetExtension(FullPath).ToLowerInvariant();

    /// <summary>
    ///     Builds the tree.
    /// </summary>
    /// <param name="filePaths"></param>
    /// <returns></returns>
    public static ObservableCollection<PackageEntry> BuildTree(IEnumerable<string> filePaths)
    {
        var root = new ObservableCollection<PackageEntry>();

        var rootNode = new PackageEntry
        {
            Name = "Root",
            FullPath = string.Empty,
            IsFolder = true
        };
        root.Add(rootNode);

        // Index nodes by full path for O(1) lookups, instead of scanning each
        // level's children linearly per segment (which made the whole build O(n²)
        // for large PAKs).
        var nodeIndex = new Dictionary<string, PackageEntry>(StringComparer.Ordinal)
        {
            [string.Empty] = rootNode
        };

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var currentNodes = rootNode.Children;
            var accumulatedPath = new StringBuilder();

            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i];
                var isFile = i == parts.Length - 1;

                if (accumulatedPath.Length > 0)
                    accumulatedPath.Append('/');
                accumulatedPath.Append(name);

                var fullPath = accumulatedPath.ToString();
                if (!nodeIndex.TryGetValue(fullPath, out var node))
                {
                    node = new PackageEntry
                    {
                        Name = name,
                        FullPath = fullPath,
                        IsFolder = !isFile
                    };
                    currentNodes.Add(node);
                    nodeIndex[fullPath] = node;
                }

                if (!isFile)
                    currentNodes = node.Children;
            }
        }

        return root;
    }
}