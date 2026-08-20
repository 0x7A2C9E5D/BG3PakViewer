using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace BG3PakViewer.Shared.Models;

public class PackageEntry
{
    public required string Name { get; init; }

    public required string FullPath { get; init; }

    public bool IsFolder { get; set; }

    public ObservableCollection<PackageEntry> Children { get; } = [];

    public string FileExtension => Path.GetExtension(FullPath).ToLowerInvariant();

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