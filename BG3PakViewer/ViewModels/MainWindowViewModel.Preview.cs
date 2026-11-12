using BG3PakViewer.Shared.Models;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Previewing the selected node and releasing the preview view model that is being replaced.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Preview async
    /// </summary>
    /// <param name="toPreview"></param>
    // Preview is deliberately serial: the command is not allowed to run concurrently, so a preview
    // that is still being built keeps the next one from starting instead of racing it. What keeps
    // the window responsive is the handlers doing the expensive work off the UI thread, not
    // overlapping previews. Disposing first means the pane empties as soon as the selection moves.
    [RelayCommand]
    private async Task PreviewAsync(object toPreview)
    {
        if (toPreview is not PackageEntry node || !_packageService.IsLoaded)
            return;

        await DisposePreviewVmAsync();

        if (node.IsFolder) return;

        await ShowPreviewAsync(node);
    }

    /// <summary>
    ///     Show preview async
    /// </summary>
    /// <param name="node"></param>
    private async Task ShowPreviewAsync(PackageEntry node)
    {
        try
        {
            Log.Information("Creating preview for: {Path}", node.FullPath);
            var viewModel = await _previewService.CreatePreviewViewModelAsync(node);
            if (viewModel != null) Log.Debug("Preview displayed successfully");
            PreviewVm = viewModel;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing preview for: {Path}", node.FullPath);
            await DisposePreviewVmAsync();
        }
    }

    /// <summary>
    ///     Clears the preview binding first so the pane empties immediately, then releases the view
    ///     model (async or sync disposable).
    /// </summary>
    private async Task DisposePreviewVmAsync()
    {
        await DisposePreviewAsync(PreviewVm);
        PreviewVm = null;
    }

    /// <summary>
    ///     Releases a preview view model.
    /// </summary>
    /// <param name="viewModel"></param>
    private static async Task DisposePreviewAsync(object? viewModel)
    {
        switch (viewModel)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}