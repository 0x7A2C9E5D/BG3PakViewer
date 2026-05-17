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
    IEnumerable<IPreviewHandler> previewHandlers,
    IEnumerable<IMultiStreamPreviewHandler> multiStreamHandlers)
    : IPreviewService
{
    private readonly List<IMultiStreamPreviewHandler> _multiStreamHandlers = multiStreamHandlers.ToList();
    private readonly List<IPreviewHandler> _previewHandlers = previewHandlers.ToList();
    private bool _disposed;

    public async Task<object?> CreatePreviewViewModelAsync(PackageEntry node)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(PreviewService));

        if (node.IsFolder) return null;

        var validation = ValidatePreviewRequest(node);
        if (validation != null) return validation;

        Log.Debug("Creating preview for: {FileName} (Extension: {Extension})",
            Path.GetFileName(node.FullPath), node.FileExtension);

        var singleStreamResult = await TrySingleStreamPreviewAsync(node);
        if (singleStreamResult != null) return singleStreamResult;

        var multiStreamResult = await TryMultiStreamPreviewAsync(node);
        if (multiStreamResult != null) return multiStreamResult;

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

    private NotSupportedFileViewModel? ValidatePreviewRequest(PackageEntry node)
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

    private async Task<object?> TrySingleStreamPreviewAsync(PackageEntry node)
    {
        var handler = _previewHandlers.FirstOrDefault(h => h.CanHandle(node.FileExtension));
        if (handler == null) return null;

        try
        {
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

    private async Task<object?> TryMultiStreamPreviewAsync(PackageEntry node)
    {
        var handler = _multiStreamHandlers.FirstOrDefault(h => h.CanHandle(node.FileExtension));
        if (handler == null) return null;
        try
        {
            var streams = await CollectStreamsAsync(handler, node);

            if (streams.Count <= 1)
            {
                await DisposeStreams(streams);
                Log.Information("No related files found for multi-stream preview: {FileName}",
                    Path.GetFileName(node.FullPath));
                return null;
            }

            var viewModel = await handler.CreatePreviewViewModelAsync(streams);
            await DisposeStreams(streams);

            if (viewModel != null)
            {
                Log.Debug("Multi-stream preview created successfully for: {FileName}",
                    Path.GetFileName(node.FullPath));
                return viewModel;
            }

            Log.Warning("Multi-stream handler returned null for: {FileName}",
                Path.GetFileName(node.FullPath));
            return null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error creating multi-stream preview for: {FileName}",
                Path.GetFileName(node.FullPath));
            return null;
        }
    }

    private async Task<Dictionary<string, Stream>> CollectStreamsAsync(IMultiStreamPreviewHandler handler,
        PackageEntry node)
    {
        return await Task.Run(() =>
        {
            var streams = new Dictionary<string, Stream>();
            var mainFile = packageService.GetFileByPath(node.FullPath);
            if (mainFile != null) streams[node.FileExtension] = mainFile.CreateContentReader();
            var relatedPatterns = handler.GetRelatedFilePatterns(node.FullPath);
            foreach (var pattern in relatedPatterns)
            {
                var relatedFile = packageService.GetFileByPath(pattern);
                if (relatedFile == null) continue;
                var extension = Path.GetExtension(pattern);
                streams[extension] = relatedFile.CreateContentReader();
                Log.Debug("Collected related file for preview: {Path}", pattern);
            }

            return streams;
        });
    }

    private static async Task DisposeStreams(Dictionary<string, Stream> streams)
    {
        foreach (var stream in streams.Values) await stream.DisposeAsync();
    }

    private static NotSupportedFileViewModel CreateNotSupportedViewModel(string helpText)
    {
        return new NotSupportedFileViewModel { HelpText = helpText };
    }
}