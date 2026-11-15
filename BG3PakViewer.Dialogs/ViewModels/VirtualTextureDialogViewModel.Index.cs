using System.Globalization;
using System.Text;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Indexing the package's tile sets, filling the texture list in batches and reporting the tile
///     sets that could not be read. Its cancellation source also stops a run when the dialog is closed.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
    private CancellationTokenSource? _indexCancellation;

    /// <summary>
    ///     Whether the package's virtual textures are being indexed.
    /// </summary>
    [ObservableProperty]
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

    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnIsIndexingChanged(bool value)
    {
        ExportSelectedCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsBusy));
    }

    /// <summary>
    ///     Indexes the package's tile sets, then fills the texture list in batches.
    /// </summary>
    private async Task IndexAsync()
    {
        const int batchSize = 64;
        var cts = new CancellationTokenSource();
        _indexCancellation = cts;
        IsIndexing = true;
        VirtualTextureIndexResult? result = null;
        var batch = new List<VirtualTextureItemViewModel>(batchSize);

        try
        {
            var progress = new Progress<VirtualTextureIndexProgress>(value =>
            {
                if (cts.IsCancellationRequested) return;
                IndexedCount = value.indexed;
                IndexTotal = value.total;
            });

            Textures.Clear();
            result = await _catalog.IndexAsync(progress, cts.Token);
            if (cts.IsCancellationRequested) return;

            foreach (var entry in result.entries)
            {
                if (cts.IsCancellationRequested) break;
                batch.Add(new VirtualTextureItemViewModel(entry, _clipboardService));
                if (batch.Count < batchSize) continue;
                PublishBatch(batch);
                // Publishing a full batch without awaiting can monopolize the dispatcher: give it a
                // turn so the rows render and the dialog stays responsive.
                await Task.Yield();
            }

            // The dialog may have been closed while the index was built: publishing the rows then
            // would touch a disposed view model and start a preview for it.
            if (cts.IsCancellationRequested) return;
            PublishBatch(batch);

            if (!string.IsNullOrWhiteSpace(InitialSearch))
                SearchText = InitialSearch;
            SelectedRow ??= Textures.FirstOrDefault(texture =>
                string.IsNullOrWhiteSpace(InitialSearch) || Matches(texture, InitialSearch));
            Log.Information("Virtual texture toolbox loaded {TextureCount} textures", Textures.Count);
        }
        catch (OperationCanceledException)
        {
            // The dialog was closed before the index finished; the canceled read is discarded.
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to index virtual textures for the toolbox");
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsIndexing = false;
            if (ReferenceEquals(_indexCancellation, cts)) _indexCancellation = null;
        }

        // Shown once the overlay is gone, and only for a run that published its index: a dialog closed
        // while indexing must not pop a message for a window that is no longer there.
        if (!cts.IsCancellationRequested && result is not null)
            await ReportIndexFailuresAsync(result.failures);
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
