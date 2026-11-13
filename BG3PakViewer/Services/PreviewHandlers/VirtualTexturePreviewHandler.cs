using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Virtual texture preview handler: a tile set or one of its page files has no per-file preview;
///     both point the user at the virtual texture preview export tool.
/// </summary>
// ReSharper disable once ClassNeverInstantiated.Global
internal sealed class VirtualTexturePreviewHandler : IPreviewHandler
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTexturePreviewHandler" /> class.
    /// </summary>
    public VirtualTexturePreviewHandler()
    {
    }

    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsVirtualTexture(fileExtension) ||
               FileExtensions.IsVirtualTexturePage(fileExtension);
    }

    /// <summary>
    ///     Create the prompt that points at the virtual texture preview export tool. A page file name
    ///     carries the texture name it belongs to, so the prompt can open the tool on that texture.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        return Task.FromResult<object?>(new VirtualTexturePromptViewModel(GetTextureName(node)));
    }

    /// <summary>
    ///     Reads the texture name from a page file name such as
    ///     <c>Albedo_Normal_Physical_3_37427012aeb5fb227a40100168e8e456.gtp</c>.
    /// </summary>
    /// <param name="node">The previewed file.</param>
    /// <returns>The texture name, or <see langword="null" /> when the file does not name one.</returns>
    private static string? GetTextureName(PackageEntry node)
    {
        if (!FileExtensions.IsVirtualTexturePage(node.FileExtension)) return null;

        var name = Path.GetFileNameWithoutExtension(node.FullPath);
        var separator = name.LastIndexOf('_');
        if (separator < 0) return null;

        var textureName = name[(separator + 1)..];
        return textureName.Length == 32 && textureName.All(Uri.IsHexDigit) ? textureName : null;
    }
}