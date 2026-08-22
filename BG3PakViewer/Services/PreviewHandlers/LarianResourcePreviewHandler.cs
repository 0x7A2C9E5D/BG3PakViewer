using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class LarianResourcePreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsLarianResource(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var resource = await ResourceLoader.LoadAsync(stream, fileExtension);
        if (resource == null) return null;

        // Build the tree off the UI thread so large resources don't block the UI.
        // Attributes are formatted lazily per selected node. No LSX string is
        // materialized for previewing.
        return await Task.Run(() => LarianResourcePreviewViewModel.FromResource(resource));
    }
}