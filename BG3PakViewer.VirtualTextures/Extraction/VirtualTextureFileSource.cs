using BG3PakViewer.Contracts;

namespace BG3PakViewer.VirtualTextures.Extraction;

/// <summary>
///     Opens the files of the virtual textures from the loaded package.
/// </summary>
/// <remarks>
///     The source holds the package it reads from and nothing else. Which file to read is an argument of
///     the call, and the path of that file is composed elsewhere.
/// </remarks>
/// <param name="packageService">The package the files are read from.</param>
internal sealed class VirtualTextureFileSource(IPackageService packageService)
{
    /// <summary>
    ///     Opens one file of the package.
    /// </summary>
    /// <param name="packagePath">The package path of the file.</param>
    /// <returns>The content of the file.</returns>
    /// <exception cref="FileNotFoundException">The package does not contain the file.</exception>
    public Stream Open(string packagePath)
    {
        return packageService.GetFileByPath(packagePath)?.CreateContentReader()
               ?? throw new FileNotFoundException($"Package file not found: {packagePath}");
    }
}
