using System.Collections.ObjectModel;
using System.IO;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Messaging;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using Serilog;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     RecentDialogViewModel
/// </summary>
/// <param name="recentFilesService"></param>
/// <param name="dialogService"></param>
public partial class RecentDialogViewModel(IRecentFilesService recentFilesService, IDialogService dialogService)
    : DisposableViewModel, IModalDialogViewModel, ICloseable
{
    /// <summary>
    ///     Recent items.
    /// </summary>
    public ObservableCollection<IRecentFileEntry> RecentItems => recentFilesService.RecentItems;

    /// <summary>
    ///     Request close.
    /// </summary>
    public event EventHandler? RequestClose;

    /// <summary>
    ///     Dialog result.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    ///     Open
    /// </summary>
    /// <param name="recentFileEntry"></param>
    [RelayCommand]
    private async Task Open(IRecentFileEntry recentFileEntry)
    {
        if (!File.Exists(recentFileEntry.FilePath))
            await HandleMissingFile(recentFileEntry);
        else
            HandleExistingFile(recentFileEntry);
    }

    /// <summary>
    ///     Handle existing file.
    /// </summary>
    /// <param name="recentFileEntry"></param>
    private void HandleExistingFile(IRecentFileEntry recentFileEntry)
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
        _ = WeakReferenceMessenger.Default.Send(new AsyncRequestMessage<string, bool>(recentFileEntry.FilePath),
            MessageTokens.RecentFileOpened);
    }

    /// <summary>
    ///     Handle missing file.
    /// </summary>
    /// <param name="recentFileEntry"></param>
    private async Task HandleMissingFile(IRecentFileEntry recentFileEntry)
    {
        LogMissingFile(recentFileEntry.FilePath);
        if (await dialogService.MessageBoxConfirmAsync(this, Strings.FileOpenedNoFoundMessage,
                Strings.FileOpenedNoFoundCaption))
            recentFilesService.RemoveRecentFile(recentFileEntry);
    }

    /// <summary>
    ///     Log missing file.
    /// </summary>
    /// <param name="filePath"></param>
    private static void LogMissingFile(string filePath)
    {
        // The user is asked whether to drop the entry, so this is an expected state, not a failure.
        Log.Warning("The file {Path} of the recent item being opened no longer exists.", filePath);
    }
}