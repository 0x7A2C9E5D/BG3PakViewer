namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Composes the package paths of the files that make up a virtual texture.
/// </summary>
/// <remarks>
///     A tile set and its page files share one directory inside the package, and the tile set records its
///     page files by file name only, so a page file is addressed relative to the tile set it belongs to.
/// </remarks>
internal static class VirtualTexturePackagePath
{
    /// <summary>
    ///     Composes the package path of a page file, which sits next to its tile set.
    /// </summary>
    /// <param name="gtsPath">The package path of the tile set the page file sits next to.</param>
    /// <param name="pageFileName">The name of the page file, as the tile set records it.</param>
    /// <returns>The package path of the page file.</returns>
    public static string GetPageFilePath(string gtsPath, string pageFileName)
    {
        var directory = Path.GetDirectoryName(gtsPath)?.Replace('\\', '/');
        return string.IsNullOrEmpty(directory) ? pageFileName : $"{directory}/{pageFileName}";
    }
}
