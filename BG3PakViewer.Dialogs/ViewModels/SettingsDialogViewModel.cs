using System.Globalization;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     SettingsDialogViewModel
/// </summary>
/// <param name="settingsManagerService"></param>
/// <param name="dialogService"></param>
public partial class SettingsDialogViewModel(
    ISettingsManagerService settingsManagerService,
    IDialogService dialogService)
    : ObservableObject, IModalDialogViewModel
{
    /// <summary>
    ///     Settings.
    /// </summary>
    public IAppSettings Settings => settingsManagerService.CurrentSettings;

    /// <summary>
    ///     Supported cultures.
    /// </summary>
    public IEnumerable<CultureInfo> SupportedCultures => settingsManagerService.SupportedCultures;

    /// <summary>
    ///     Dialog result.
    /// </summary>
    public bool? DialogResult => true;

    /// <summary>
    ///     Set default open directory.
    /// </summary>
    [RelayCommand]
    private async Task SetDefaultOpenDirectoryAsync()
    {
        var folder = await dialogService.ShowOpenFolderDialogAsync(this);
        if (folder != null)
            Settings.DefaultOpenDirectory = folder.LocalPath;
    }

    /// <summary>
    ///     Set default export directory.
    /// </summary>
    [RelayCommand]
    private async Task SetDefaultExportDirectoryAsync()
    {
        var folder = await dialogService.ShowOpenFolderDialogAsync(this);
        if (folder != null)
            Settings.DefaultExportDirectory = folder.LocalPath;
    }

    /// <summary>
    ///     Reset default open directory.
    /// </summary>
    [RelayCommand]
    private void ResetDefaultOpenDirectory()
    {
        Settings.DefaultOpenDirectory = string.Empty;
    }
    
    /// <summary>
    ///     Reset default export directory.
    /// </summary>
    [RelayCommand]
    private void ResetDefaultExportDirectory()
    {
        Settings.DefaultExportDirectory = string.Empty;
    }
}