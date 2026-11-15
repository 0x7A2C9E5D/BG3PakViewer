using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Tracking the rows the list has selected, and checking asynchronously whether they hold any
///     layer to export so that an empty selection cannot be offered to the export command.
/// </summary>
public partial class VirtualTextureToolboxDialogViewModel
{
    private List<VirtualTextureToolboxTextureViewModel> _selectedTextures = [];
    private CancellationTokenSource? _selectionCheckCancellation;
    private bool _selectionHasData;

    /// <summary>
    ///     The number of textures selected in the list.
    /// </summary>
    [ObservableProperty]
    private partial int SelectedCount { get; set; }

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedCountChanged(int value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Updates the textures the export writes from the list selection.
    /// </summary>
    /// <param name="selected">The textures selected in the list.</param>
    public void UpdateSelection(IEnumerable<VirtualTextureToolboxTextureViewModel> selected)
    {
        _selectedTextures = [.. selected];
        SelectedCount = _selectedTextures.Count;
        _ = CheckSelectionAsync();
    }

    /// <summary>
    ///     Finds out whether the selection holds any layer to export, so that a selection which is
    ///     empty in every layer cannot be exported at all.
    /// </summary>
    private async Task CheckSelectionAsync()
    {
        var cts = await BeginSelectionCheckAsync();

        try
        {
            _selectionHasData = await TryAnySelectedLayerHasDataAsync(cts.Token);
        }
        finally
        {
            EndSelectionCheck(cts);
        }
    }

    /// <summary>
    ///     Runs the layer-data check, treating a cancellation and a failure as "no data" so the export
    ///     stays disabled instead of offering files that cannot be written.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when one selected layer holds data.</returns>
    private async Task<bool> TryAnySelectedLayerHasDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await AnySelectedLayerHasDataAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // A newer selection replaced this check.
            return false;
        }
        catch (Exception ex)
        {
            // Failing to prove the selection has data keeps the export disabled, which is the safe side.
            Log.Error(ex, "Failed to check whether the selected textures hold any layer data");
            return false;
        }
    }

    /// <summary>
    ///     Starts a new selection check, cancelling the one it replaces, and keeps the export disabled
    ///     until the check proves the selection holds data.
    /// </summary>
    /// <returns>The cancellation source of the new check.</returns>
    private async Task<CancellationTokenSource> BeginSelectionCheckAsync()
    {
        if (_selectionCheckCancellation is not null) await _selectionCheckCancellation.CancelAsync();

        var cts = new CancellationTokenSource();
        _selectionCheckCancellation = cts;
        // Off until the check proves otherwise: the command must not offer an export that has
        // nothing to write.
        _selectionHasData = false;
        ExportSelectedCommand.NotifyCanExecuteChanged();

        return cts;
    }

    /// <summary>
    ///     Re-enables the export for the check that finished, unless a newer check replaced it already.
    /// </summary>
    /// <param name="cts">The cancellation source of the check that finished.</param>
    private void EndSelectionCheck(CancellationTokenSource cts)
    {
        if (!ReferenceEquals(_selectionCheckCancellation, cts)) return;

        ExportSelectedCommand.NotifyCanExecuteChanged();
        _selectionCheckCancellation = null;
    }

    /// <summary>
    ///     Finds out whether any selected texture holds a layer to export.
    /// </summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when at least one selected layer holds data.</returns>
    private async Task<bool> AnySelectedLayerHasDataAsync(CancellationToken cancellationToken)
    {
        // Grouped by tile set so the cached one is parsed as few times as possible.
        var textures = _selectedTextures.OrderBy(texture => texture.Entry.path, StringComparer.Ordinal).ToList();
        for (var layer = 0; layer < VirtualTextureLayers.Names.Count; layer++)
            if (await AnyTextureHasLayerDataAsync(textures, layer, cancellationToken))
                return true;

        return false;
    }

    /// <summary>
    ///     Finds out whether one of the textures holds a layer to export at the given layer index.
    /// </summary>
    /// <param name="textures">The selected textures, ordered by tile set.</param>
    /// <param name="layer">The layer index to check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true" /> when one texture holds data in that layer.</returns>
    private async Task<bool> AnyTextureHasLayerDataAsync(
        IReadOnlyList<VirtualTextureToolboxTextureViewModel> textures, int layer, CancellationToken cancellationToken)
    {
        foreach (var texture in textures.Where(texture =>
                     layer < Math.Min(texture.Entry.layerCount, VirtualTextureLayers.Names.Count)))
            if (await _extractor.HasLayerAsync(texture.Entry, layer, cancellationToken))
                return true;

        return false;
    }
}
