using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Search commands: the title bar box that can target the active preview as well as the package
///     tree, and the box above the tree that always filters the tree.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Broadcasts the search query so the active preview (virtual texture list / localization
    ///     grid) handles it via the messenger; with no preview open it searches the package tree.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync(string query)
    {
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(query), MessageTokens.SearchQueryChanged);
        if (PreviewVm is not null) return;

        if (!_packageService.IsLoaded) return;

        await Task.Run(() =>
        {
            Log.Information("Searching for '{Query}'...", query);
            PackageTree = _packageService.BuildTree(query);
            Log.Information("Search completed");
        }, CancellationToken.None);
    }

    /// <summary>
    ///     Only clears the search state (full list / full tree) when the text was emptied.
    /// </summary>
    [RelayCommand]
    private async Task ClearSearchAsync(string query)
    {
        if (!string.IsNullOrWhiteSpace(query)) return;
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<string?>(null), MessageTokens.SearchQueryChanged);
        if (PreviewVm is not null) return;

        if (!_packageService.IsLoaded) return;
        await Task.Run(() => { PackageTree = _packageService.BuildTree(); }, CancellationToken.None);
        Log.Information("Search query cleared");
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