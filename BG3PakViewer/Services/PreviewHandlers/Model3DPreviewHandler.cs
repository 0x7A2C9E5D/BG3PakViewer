using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using Serilog;

namespace BG3PakViewer.Services.PreviewHandlers;

/// <summary>
///     Model 3D preview handler
/// </summary>
/// <param name="packageService"></param>
internal class Model3DPreviewHandler(IPackageService packageService) : IPreviewHandler
{
    /// <summary>
    ///     Can handle
    /// </summary>
    /// <param name="fileExtension"></param>
    /// <returns></returns>
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsModel3DFormat(fileExtension);
    }

    /// <summary>
    ///     Create preview view model async
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        await using var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
        if (stream is null) return null;

        var root = await Model3DLoader.LoadAsync(stream);

        if (root == null)
            return null;
        if (root.Meshes != null && root.Meshes.Count != 0)
            return new Model3DPreviewViewModel { Model = root };
        Log.Warning("Model has no meshes");
        return new NotSupportedPreviewViewModel
        {
            HelpText = Strings.ModelMissingMeshes
        };
    }
}