using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Dialogs.Extensions;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

public partial class RecentDialogViewModel : DisposableViewModel, IModalDialogViewModel, ICloseable
{
    private readonly IDialogService _dialogService;
    private readonly IRecentFilesService _recentFilesService;

    public RecentDialogViewModel(IRecentFilesService recentFilesService, IDialogService dialogService)
    {
        _recentFilesService = recentFilesService;
        _dialogService = dialogService;
        _recentFilesService.RecentItems
            .CollectionChanged += OnRecentItemsOnCollectionChanged;
    }

    public ObservableCollection<IRecentFileEntry> RecentItems => _recentFilesService.RecentItems;

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
    private async Task Open(IRecentFileEntry recentFileEntry)
    {
        if (!File.Exists(recentFileEntry.FilePath))
            await HandleMissingFile(recentFileEntry);
        else
            HandleExistingFile(recentFileEntry);
    }

    private void HandleExistingFile(IRecentFileEntry recentFileEntry)
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
        _ = WeakReferenceMessenger.Default.Send(new AsyncRequestMessage<string, bool>(recentFileEntry.FilePath),
            MessageTokens.RecentFileOpened);
    }

    private async Task HandleMissingFile(IRecentFileEntry recentFileEntry)
    {
        LogMissingFile(recentFileEntry.FilePath);
        if (await _dialogService.ConfirmAsync(this, Strings.FileOpenedNoFoundMessage,
                Strings.FileOpenedNoFoundCaption))
            _recentFilesService.RemoveRecentFile(recentFileEntry);
    }

    private static void LogMissingFile(string filePath)
    {
        Log.Error("The file {Path} for the recent item being opened does not exist.", filePath);
    }
}