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
    ///     Create the prompt that points at the virtual texture preview export tool.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        return Task.FromResult<object?>(new VirtualTexturePromptViewModel(Path.GetFileName(node.FullPath)));
    }
}
