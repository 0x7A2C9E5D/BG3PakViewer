using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

public partial class RecentDialogViewModel : DisposableViewModel, IModalDialogViewModel, ICloseable
{
    private readonly IRecentFilesService _recentFilesService;

    public RecentDialogViewModel(IRecentFilesService recentFilesService)
    {
        _recentFilesService = recentFilesService;
        _recentFilesService.RecentItems
            .CollectionChanged += OnRecentItemsOnCollectionChanged;
    }

    public ObservableCollection<IRecentItem> RecentItems => _recentFilesService.RecentItems;

    public event EventHandler? RequestClose;

    public bool? DialogResult => true;

    private static void OnRecentItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Remove)
            Log.Information("Recent items collection changed: {Count} items removed.",
                e.OldItems?.Count ?? 0);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _recentFilesService.RecentItems.CollectionChanged -= OnRecentItemsOnCollectionChanged;
    }

    [RelayCommand]
    private async Task Open(IRecentItem recentItem)
    {
        if (!File.Exists(recentItem.FilePath))
            await HandleMissingFile(recentItem);
        else
            HandleExistingFile(recentItem);
    }

    private void HandleExistingFile(IRecentItem recentItem)
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
        _ = WeakReferenceMessenger.Default.Send(new AsyncRequestMessage<string, bool>(recentItem.FilePath),
            MessageTokens.RecentFileOpened);
    }

    private async Task HandleMissingFile(IRecentItem recentItem)
    {
        LogMissingFile(recentItem.FilePath);
        if (await NotifyFileNotFound(recentItem.FilePath))
            _recentFilesService.RemoveRecentFile(recentItem);
    }

    private static async Task<bool> NotifyFileNotFound(string filePath)
    {
        return await WeakReferenceMessenger.Default.Send(new AsyncRequestMessage<string, bool>(filePath),
            MessageTokens.OpenedFileNoFound);
    }

    private static void LogMissingFile(string filePath)
    {
        Log.Error("The file {Path} for the recent item being opened does not exist.", filePath);
    }
}