using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Search commands, one pair per search box. The title bar box filters the active preview only
///     (virtual texture list, localization grid); the box above the tree filters the package tree
///     only. Neither box may touch what the other one owns.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Broadcasts the search query so the active preview handles it via the messenger. The
    ///     package tree is left untouched: it belongs to the tree search box.
    /// </summary>
    [RelayCommand]
    private void SearchAsync(string query)
    {
        Log.Information("Searching the preview for '{Query}'...", query);
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(query), MessageTokens.SearchQueryChanged);
    }

    /// <summary>
    ///     Clears the preview filter when the title bar box is emptied.
    /// </summary>
    [RelayCommand]
    private void ClearSearchAsync(string query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(null), MessageTokens.SearchQueryChanged);
        Log.Information("Preview search query cleared");
    }

    /// <summary>
    ///     Filters the package tree from the search box above the tree. It deliberately ignores the
    ///     active preview: that box always searches the tree, the window title bar box is the one
    ///     that targets the preview.
    /// </summary>
    [RelayCommand]
    private async Task SearchTreeAsync(string query)
    {
        if (!_packageService.IsLoaded) return;

        await Task.Run(() =>
        {
            Log.Information("Searching package tree for '{Query}'...", query);
            PackageTree = _packageService.BuildTree(query);
            Log.Information("Package tree search completed");
        }, CancellationToken.None);
    }

    /// <summary>
    ///     Restores the full package tree when the tree search box is emptied.
    /// </summary>
    [RelayCommand]
    private async Task ClearTreeSearchAsync(string query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        if (!_packageService.IsLoaded) return;

        await Task.Run(() => { PackageTree = _packageService.BuildTree(); }, CancellationToken.None);
        Log.Information("Package tree search query cleared");
    }
}