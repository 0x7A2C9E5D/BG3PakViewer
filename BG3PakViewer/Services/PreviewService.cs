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
    private readonly List<IPreviewHandler> _previewHandlers = previewHandlers.ToList();
    private bool _disposed;

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PreviewService));
        if (node.IsFolder)
            return null;

        var fileName = Path.GetFileName(node.FullPath);

        if (FileExtensions.IsLowTexTexture(fileName))
        {
            Log.Information("Low resolution DDS file is not supported for preview: {FileName}", fileName);
            return new NotSupportedFileViewModel
            {
                HelpText = Strings.LowTexDDSNotSupportedMessage
            };
        }

        var file = packageService.GetFileByPath(node.FullPath);
        if (file == null)
        {
            Log.Warning("Preview file not found: {Path}", node.FullPath);
            return new NotSupportedFileViewModel
            {
                HelpText = Strings.LoadResourceFailed
            };
        }

        Log.Debug("Creating preview for: {FileName} (Extension: {Extension})", fileName, node.FileExtension);

        var handler = _previewHandlers.FirstOrDefault(h => h.CanHandle(node.FileExtension));

        if (handler == null)
        {
            Log.Information("No preview handler found for extension: {Extension}", node.FileExtension);
            return new NotSupportedFileViewModel
            {
                HelpText = Strings.FileNotSupportedPreviewMessage
            };
        }

        try
        {
            await using var stream = file.CreateContentReader();
            var viewModel = await handler.CreatePreviewViewModelAsync(stream, node.FileExtension);

            if (viewModel != null)
            {
                Log.Debug("Preview created successfully for: {FileName}", fileName);
                return viewModel;
            }

            Log.Warning("Preview handler returned null for: {FileName}", fileName);
            return new NotSupportedFileViewModel
            {
                HelpText = Strings.LoadResourceFailed
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating preview for: {FileName}", fileName);
            return new NotSupportedFileViewModel
            {
                HelpText = Strings.LoadResourceFailed
            };
        }
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
}