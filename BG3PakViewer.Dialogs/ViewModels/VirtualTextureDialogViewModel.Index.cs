using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.VirtualTextures.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     Loading the package's virtual texture index, installing the rows in one refresh and reporting
///     the tile sets that could not be read. The catalog answers from its cache when the package has
///     not changed, so no tile set is read on that path. Its cancellation source also stops a run
///     when the dialog is closed.
/// </summary>
public partial class VirtualTextureDialogViewModel
{
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
    ///     Indexes the package's tile sets, installs the rows in one refresh and, once the overlay is
    ///     gone, reports the tile sets the index had to skip.
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
            // The dialog may have been closed while the index was built: publishing the rows then
            // would touch a disposed view model and start a preview for it.
            if (!cts.IsCancellationRequested) PublishIndex(result);
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

        return _catalog.IndexAsync(progress, cts.Token);
    }

    /// <summary>
    ///     Builds the texture items in a plain list and installs them in one refresh, so the list view is
    ///     notified once instead of once per texture, then applies the initial search.
    /// </summary>
    /// <param name="result">The index whose texture items are published.</param>
    private void PublishIndex(VirtualTextureIndexResult result)
    {
        var textureItems = new List<VirtualTextureItemViewModel>(result.entries.Count);
        textureItems.AddRange(result.entries.Select(entry =>
            new VirtualTextureItemViewModel(entry, _clipboardService)));

        Textures = [.. textureItems];
        ApplyInitialSearch();
        Log.Information("Virtual texture toolbox loaded {TextureCount} textures", Textures.Count);
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
    ///     Tells the user which tile sets the index had to skip, so that a tile set which no longer parses
    ///     is not mistaken for one that holds no textures.
    /// </summary>
    /// <param name="failures">The tile sets that could not be read.</param>
    [SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'",
        Justification = "The format template is localized; caching would pin it to one language.")]
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
