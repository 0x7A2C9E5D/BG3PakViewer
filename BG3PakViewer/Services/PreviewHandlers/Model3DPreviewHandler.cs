using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using Serilog;

namespace BG3PakViewer.Services.PreviewHandlers;

internal class Model3DPreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsModel3DFormat(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        var root = await Model3DLoader.LoadAsync(stream);

        if (root == null)
            return null;
        if (root.Meshes != null && root.Meshes.Count != 0)
            return new Model3DFileViewModel { Data = root };
        Log.Warning("Model has no meshes");
        return new NotSupportedFileViewModel
        {
            HelpText = Strings.ModelMissingMeshes
        };
    }
}