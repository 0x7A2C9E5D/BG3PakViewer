using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.VirtualTextures;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class GtsPreviewHandler(IPackageService packageService) : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
        => fileExtension.Equals(".gts", StringComparison.OrdinalIgnoreCase);

    public Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
        => Task.FromResult<object?>(null);

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        return await Task.Run(async () =>
        {
            var directory = Path.GetDirectoryName(node.FullPath)?.Replace('\\', '/') ?? string.Empty;
            var gtsStream = packageService.GetFileByPath(node.FullPath)?.CreateContentReader();
            if (gtsStream is null) return await Task.FromResult<object?>(null);

            IReadOnlyList<string> pageFileNames = [];
            VirtualTileSetExtractor? extractor = null;
            try
            {
                extractor = new VirtualTileSetExtractor(gtsStream, pageFileIndex =>
                {
                    // ReSharper disable once AccessToModifiedClosure
                    var pageFileName = pageFileNames[pageFileIndex];
                    var pagePath = directory.Length == 0
                        ? pageFileName
                        : $"{directory}/{pageFileName}";
                    return packageService.GetFileByPath(pagePath)?.CreateContentReader()
                           ?? throw new FileNotFoundException($"GTP page file not found in package: {pagePath}");
                });
                pageFileNames = extractor.PageFileNames;
                return new GtsPreviewViewModel(extractor);
            }
            catch
            {
                extractor?.Dispose();
                throw;
            }
        });
    }
}
