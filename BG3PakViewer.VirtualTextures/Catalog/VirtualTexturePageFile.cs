using BG3PakViewer.Contracts;

namespace BG3PakViewer.VirtualTextures.Catalog;

/// <summary>
///     Opens the GTP page files of a virtual texture tile set that lives inside the package.
/// </summary>
internal static class VirtualTexturePageFile
{
    /// <summary>
    ///     Creates the provider that opens the page files of the tile set at <paramref name="gtsPath" />.
    /// </summary>
    /// <param name="packageService">The service the pages are read from.</param>
    /// <param name="gtsPath">The package path of the tile set the page files sit next to.</param>
    /// <returns>The provider a loader opens its page files through.</returns>
    public static Func<string, Stream> CreateProvider(IPackageService packageService, string gtsPath)
    {
        var directory = Path.GetDirectoryName(gtsPath)?.Replace('\\', '/') ?? string.Empty;
        return pageFileName => Open(packageService, directory, pageFileName);
    }

    /// <summary>
    ///     Opens the page file at the given name, which sits next to the tile set in the package.
    /// </summary>
    /// <param name="packageService">The service the page is read from.</param>
    /// <param name="directory">The package directory of the tile set, with forward slashes.</param>
    /// <param name="pageFileName">The name of the page file, as referenced by the tile set.</param>
    /// <returns>The content of the page file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the page file.</exception>
    public static Stream Open(IPackageService packageService, string directory, string pageFileName)
    {
        var pagePath = directory.Length == 0 ? pageFileName : $"{directory}/{pageFileName}";
        return packageService.GetFileByPath(pagePath)?.CreateContentReader()
               ?? throw new FileNotFoundException($"GTP page file not found in package: {pagePath}");
    }
}