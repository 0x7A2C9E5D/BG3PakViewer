using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class VirtualTexturePreviewHandler(IPackageService packageService) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return fileExtension.Equals(".gts", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        return await Task.Run<object?>(() =>
        {
            var gtsStream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
            if (gtsStream is null) return null;

            return new VirtualTexturePreviewViewModel(
                VirtualTextureLoaderFactory.Create(packageService, node.FullPath, gtsStream));
        });
    }
}
