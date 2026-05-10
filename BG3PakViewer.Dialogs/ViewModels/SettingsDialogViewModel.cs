using System.Globalization;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

public partial class SettingsDialogViewModel(
    ISettingsManagerService settingsManagerService,
    IDialogService dialogService)
    : ObservableObject, IModalDialogViewModel
{
    public IAppSettings Settings => settingsManagerService.CurrentSettings;

    public IEnumerable<CultureInfo> SupportedCultures => settingsManagerService.SupportedCultures;

    public bool? DialogResult => true;

    [RelayCommand]
    private async Task SetDefaultOpenDirectoryAsync()
    {
        var folder = await dialogService.ShowOpenFolderDialogAsync(this);
        if (folder != null)
            Settings.DefaultOpenDirectory = folder.LocalPath;
    }

    [RelayCommand]
    private async Task SetDefaultExportDirectoryAsync()
    {
        var folder = await dialogService.ShowOpenFolderDialogAsync(this);
        if (folder != null)
            Settings.DefaultExportDirectory = folder.LocalPath;
    }

    [RelayCommand]
    private void ResetDefaultOpenDirectory()
    {
        Settings.DefaultOpenDirectory = string.Empty;
    }

    [RelayCommand]
    private void ResetDefaultExportDirectory()
    {
        Settings.DefaultExportDirectory = string.Empty;
    }
}