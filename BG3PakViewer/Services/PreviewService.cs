using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Locales;
using BG3PakViewer.Services.PreviewHandlers;
using BG3PakViewer.Shared.Models;
using BG3PakViewer.Utils;
using Serilog;

namespace BG3PakViewer.Services;

internal class PreviewService(
    IPackageService packageService,
    IEnumerable<IPreviewHandler> previewHandlers)
    : IPreviewService
{
    private readonly List<IPreviewHandler> _previewHandlers = [.. previewHandlers];
    private bool _disposed;

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PreviewService));

        if (node.IsFolder) return null;

        var validation = ValidatePreviewRequest(node);
        if (validation != null) return validation;

        Log.Debug("Creating preview for: {FileName} (Extension: {Extension})",
            Path.GetFileName(node.FullPath), node.FileExtension);

        var singleStreamResult = await CreatePreviewAsync(node);
        if (singleStreamResult != null) return singleStreamResult;

        Log.Information("No preview handler found for extension: {Extension}", node.FileExtension);
        return CreateNotSupportedViewModel(Strings.FileNotSupportedPreviewMessage);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            Log.Debug("Disposing PreviewService");
            _disposed = true;
            await ValueTask.CompletedTask;
        }
    }

    private NotSupportedPreviewViewModel? ValidatePreviewRequest(PackageEntry node)
    {
        var fileName = Path.GetFileName(node.FullPath);
        if (FileExtensions.IsLowTexTexture(fileName))
        {
            Log.Information("Low resolution DDS file is not supported for preview: {FileName}", fileName);
            return CreateNotSupportedViewModel(Strings.LowTexDDSNotSupportedMessage);
        }

        var file = packageService.GetFileByPath(node.FullPath);
        if (file != null) return null;
        Log.Warning("Preview file not found: {Path}", node.FullPath);
        return CreateNotSupportedViewModel(Strings.LoadResourceFailed);
    }

    private async Task<object?> CreatePreviewAsync(PackageEntry node)
    {
        var handler = _previewHandlers.FirstOrDefault(h => h.CanHandle(node.FileExtension));
        if (handler == null) return null;

        try
        {
            var multiStreamViewModel = await handler.CreatePreviewViewModelAsync(node);
            if (multiStreamViewModel != null)
            {
                Log.Debug("Multi-stream preview created successfully for: {FileName}",
                    Path.GetFileName(node.FullPath));
                return multiStreamViewModel;
            }

            await using var stream = packageService.GetFileByPath(node.FullPath)!.CreateContentReader();
            var viewModel = await handler.CreatePreviewViewModelAsync(stream, node.FileExtension);

            if (viewModel != null)
            {
                Log.Debug("Single-stream preview created successfully for: {FileName}",
                    Path.GetFileName(node.FullPath));
                return viewModel;
            }

            Log.Warning("Single-stream handler returned null for: {FileName}",
                Path.GetFileName(node.FullPath));
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating single-stream preview for: {FileName}",
                Path.GetFileName(node.FullPath));
            return null;
        }
    }

    private static NotSupportedPreviewViewModel CreateNotSupportedViewModel(string helpText)
    {
        return new NotSupportedPreviewViewModel { HelpText = helpText };
    }
}