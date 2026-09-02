using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Shared.Models;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class GtsPreviewHandler(IPackageService packageService) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return fileExtension.Equals(".gts", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        return await Task.Run(async () =>
        {
            var directory = Path.GetDirectoryName(node.FullPath)?.Replace('\\', '/') ?? string.Empty;
            var stream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
            if (stream is null) return await Task.FromResult<object?>(null);

            IReadOnlyList<string> pageFileNames = [];
            VirtualTextureLoader? loader = null;
            try
            {
                loader = new VirtualTextureLoader(stream, pageFileIndex =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var pageFileName = pageFileNames[pageFileIndex];
                    var pagePath = directory.Length == 0
                        ? pageFileName
                        : $"{directory}/{pageFileName}";
                    return packageService.GetFileByPath(pagePath)?.CreateContentReader()
                           ?? throw new FileNotFoundException($"GTP page file not found in package: {pagePath}");
                });
                pageFileNames = loader.TextureNames;
                return new VirtualTexturePreviewViewModel(loader);
            }
            catch
            {
                loader?.Dispose();
                throw;
            }
        });
    }
}