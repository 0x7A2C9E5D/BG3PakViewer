using System.Globalization;
using System.Text;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Loading the package's virtual texture index, filling the texture list in batches and reporting
///     the tile sets that could not be read. The catalog answers from its cache when the package has
///     not changed, so no tile set is read on that path. Its cancellation source also stops a run
///     when the dialog is closed.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    private const int IndexBatchSize = 64;

    private CancellationTokenSource? _indexCancellation;

    /// <summary>
    ///     Whether the package's virtual textures are being indexed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyCanExecuteChangedFor(nameof(ExportAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportSearchResultsCommand))]
    public partial bool IsIndexing { get; private set; }

    /// <summary>
    ///     The number of tile sets indexed so far.
    /// </summary>
    [ObservableProperty]
    public partial int IndexedCount { get; private set; }

    /// <summary>
    ///     The number of tile sets to index.
    /// </summary>
    [ObservableProperty]
    public partial int IndexTotal { get; private set; }

    /// <summary>
    ///     Indexes the package's tile sets, publishes the rows in batches and, once the overlay is gone,
    ///     reports the tile sets the index had to skip.
    /// </summary>
    private async Task IndexAsync()
    {
        var cts = BeginIndex();
        VirtualTextureIndexResult? result;
        try
        {
            result = await TryIndexAsync(cts);
        }
        finally
        {
            EndIndex(cts);
        }

        // Shown once the overlay is gone, and only for a run that published its index: a dialog closed
        // while indexing must not pop a message for a window that is no longer there.
        if (!cts.IsCancellationRequested && result is not null)
            await ReportIndexFailuresAsync(result.failures);
    }

    /// <summary>
    ///     Starts an indexing run and exposes its cancellation source so closing the dialog can stop it.
    /// </summary>
    /// <returns>The cancellation source of the new run.</returns>
    private CancellationTokenSource BeginIndex()
    {
        var cts = new CancellationTokenSource();
        _indexCancellation = cts;
        IsIndexing = true;
        return cts;
    }

    /// <summary>
    ///     Builds and publishes the index, keeping the result even when publishing fails afterward so
    ///     the skipped tile sets are still reported.
    /// </summary>
    /// <param name="cts">Cancels the run when the dialog is closed.</param>
    /// <returns>The index, or <see langword="null" /> when the catalog did not finish.</returns>
    private async Task<VirtualTextureIndexResult?> TryIndexAsync(CancellationTokenSource cts)
    {
        VirtualTextureIndexResult? result = null;
        try
        {
            result = await LoadIndexAsync(cts);
            if (!cts.IsCancellationRequested) await PublishIndexAsync(result, cts);
        }
        catch (OperationCanceledException)
        {
            // The dialog was closed before the index finished, or the package was replaced mid-run;
            // either way the discarded index must not be published to this view model.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to index virtual textures for the toolbox");
        }

        return result;
    }

    /// <summary>
    ///     Asks the catalog for the index, reporting how many tile sets have been read so far.
    /// </summary>
    /// <param name="cts">Cancels the run when the dialog is closed.</param>
    /// <returns>The indexed textures and the tile sets that could not be read.</returns>
    private Task<VirtualTextureIndexResult> LoadIndexAsync(CancellationTokenSource cts)
    {
        var progress = new Progress<VirtualTextureIndexProgress>(value =>
        {
            if (cts.IsCancellationRequested) return;
            IndexedCount = value.indexed;
            IndexTotal = value.total;
        });

        Textures.Clear();
        return _catalog.IndexAsync(progress, cts.Token);
    }

    /// <summary>
    ///     Publishes the full batches and then the last one, applies the initial search and logs the
    ///     number of loaded textures.
    /// </summary>
    /// <param name="result">The index to publish.</param>
    /// <param name="cts">Cancels the run when the dialog is closed.</param>
    private async Task PublishIndexAsync(VirtualTextureIndexResult result, CancellationTokenSource cts)
    {
        var batch = await PublishFullBatchesAsync(result.entries, cts);

        // The dialog may have been closed while the index was built: publishing the rows then
        // would touch a disposed view model and start a preview for it.
        if (cts.IsCancellationRequested) return;
        PublishBatch(batch);
        ApplyInitialSearch();
        Log.Information("Virtual texture toolbox loaded {TextureCount} textures", Textures.Count);
    }

    /// <summary>
    ///     Publishes every full batch, giving the dispatcher a turn between them so the dialog stays
    ///     responsive while a large index is being added to the list.
    /// </summary>
    /// <param name="entries">The indexed textures, in package and tile set order.</param>
    /// <param name="cts">Cancels the run when the dialog is closed.</param>
    /// <returns>The rows of the last, not yet published batch.</returns>
    private async Task<List<VirtualTextureItemViewModel>> PublishFullBatchesAsync(
        IReadOnlyList<VirtualTextureEntry> entries, CancellationTokenSource cts)
    {
        var batch = new List<VirtualTextureItemViewModel>(IndexBatchSize);
        foreach (var entry in entries)
        {
            if (cts.IsCancellationRequested) break;
            batch.Add(new VirtualTextureItemViewModel(entry, _clipboardService));
            if (batch.Count < IndexBatchSize) continue;
            PublishBatch(batch);
            // Publishing a full batch without awaiting can monopolize the dispatcher: give it a
            // turn so the rows render and the dialog stays responsive.
            await Task.Yield();
        }

        return batch;
    }

    /// <summary>
    ///     Applies the query the toolbox was opened with and selects its first matching row.
    /// </summary>
    private void ApplyInitialSearch()
    {
        if (!string.IsNullOrWhiteSpace(InitialSearch)) SearchText = InitialSearch;
        SelectedRow ??= Textures.FirstOrDefault(texture =>
            string.IsNullOrWhiteSpace(InitialSearch) || Matches(texture, InitialSearch));
    }

    /// <summary>
    ///     Clears the busy state of a completed run, unless the dialog was closed while it ran.
    /// </summary>
    /// <param name="cts">The cancellation source of the run that ended.</param>
    private void EndIndex(CancellationTokenSource cts)
    {
        if (!cts.IsCancellationRequested) IsIndexing = false;
        if (ReferenceEquals(_indexCancellation, cts)) _indexCancellation = null;
    }

    /// <summary>
    ///     Publishes one batch of indexed rows to the list and empties it for the next batch.
    /// </summary>
    /// <param name="batch">The rows to publish; cleared once they have been added.</param>
    private void PublishBatch(List<VirtualTextureItemViewModel> batch)
    {
        // ObservableCollection must be touched on the UI thread and has no AddRange: rows are added
        // in batches so a large tile set cannot monopolize the dispatcher.
        foreach (var texture in batch) Textures.Add(texture);
        batch.Clear();
    }

    /// <summary>
    ///     Tells the user which tile sets the index had to skip, so that a tile set which no longer parses
    ///     is not mistaken for one that holds no textures.
    /// </summary>
    /// <param name="failures">The tile sets that could not be read.</param>
    private async Task ReportIndexFailuresAsync(IReadOnlyList<VirtualTextureIndexFailure> failures)
    {
        if (failures.Count == 0) return;

        var message = new StringBuilder();
        message.AppendLine(string.Format(CultureInfo.CurrentCulture, Strings.VirtualTextureIndexFailedMessage,
            failures.Count));
        message.AppendLine();
        foreach (var failure in failures)
            message.AppendLine(CultureInfo.CurrentCulture, $"{failure.path}: {failure.reason}");

        await _dialogService.ShowMessageBoxNotifyAsync(this, message.ToString().TrimEnd(),
            Strings.VirtualTextureIndexFailedCaption, MessageBoxIcon.Warning);
    }
}
