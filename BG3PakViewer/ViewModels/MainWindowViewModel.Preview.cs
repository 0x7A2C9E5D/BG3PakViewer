using BG3PakViewer.Shared.Models;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Previewing the selected node and releasing the preview view model that is being replaced.
/// </summary>
internal partial class MainWindowViewModel
{
    private PackageEntry? _pendingPreviewNode;

    /// <summary>
    ///     Preview async
    /// </summary>
    /// <param name="toPreview"></param>
    // Preview is deliberately serial: the pane shows one file and the handlers do the expensive
    // work off the UI thread, not overlapping previews. Concurrent executions are allowed so a
    // selection made while a preview is still being built is not dropped by CanExecute; it
    // replaces the pending selection and is built as soon as the current build finishes.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PreviewAsync(object toPreview)
    {
        if (toPreview is not PackageEntry node || !_packageService.IsLoaded)
            return;

        _pendingPreviewNode = node;
        if (_state.IsBuildingPreview) return;

        _state.BeginPreview();
        try
        {
            await ShowPendingPreviewAsync();
        }
        finally
        {
            _state.EndPreview(PreviewVm is not null);
        }
    }

    /// <summary>
    ///     Shows the pending preview; a selection made while it is being built replaces the pending
    ///     one, so only the newest selection is displayed and superseded view models are discarded.
    /// </summary>
    private async Task ShowPendingPreviewAsync()
    {
        while (_pendingPreviewNode is { } pending)
        {
            _pendingPreviewNode = null;
            await DisposePreviewVmAsync();
            if (pending.IsFolder) continue;

            var viewModel = await BuildPreviewVmAsync(pending);
            if (_pendingPreviewNode is null)
                PreviewVm = viewModel;
            else
                await DisposePreviewAsync(viewModel);
        }
    }

    /// <summary>
    ///     Builds the preview view model of a node.
    /// </summary>
    /// <param name="node"></param>
    /// <returns>The view model, or null when the preview could not be created.</returns>
    private async Task<object?> BuildPreviewVmAsync(PackageEntry node)
    {
        try
        {
            Log.Information("Creating preview for: {Path}", node.FullPath);
            var viewModel = await _previewService.CreatePreviewViewModelAsync(node);
            if (viewModel != null) Log.Debug("Preview created successfully");
            return viewModel;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error showing preview for: {Path}", node.FullPath);
            return null;
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
        _state.ClearPreview();
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
