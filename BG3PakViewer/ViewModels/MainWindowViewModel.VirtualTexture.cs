using System.IO;
using BG3PakViewer.Dialogs.ViewModels;
using BG3PakViewer.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     The virtual texture toolbox: detecting a package that contains tile sets and showing the
///     cross-file export dialog.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Whether the loaded package contains virtual textures.
    /// </summary>
    [ObservableProperty]
    public partial bool IsVirtualTexturePackage { get; private set; }

    /// <summary>
    ///     Shows the cross-file virtual texture export dialog.
    /// </summary>
    [RelayCommand]
    private async Task ShowVtToolboxAsync()
    {
        if (!IsVirtualTexturePackage) return;

        VtToolboxViewModel? viewModel = null;
        try
        {
            viewModel = new VtToolboxViewModel(_virtualTextureCatalog, _previewExportService, AppSettings,
                _dialogService, _clipboardService);
            await _dialogService.ShowDialogAsync(this, viewModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show the virtual texture toolbox.");
        }
        finally
        {
            viewModel?.Dispose();
        }
    }

    /// <summary>
    ///     Whether the loaded package contains at least one virtual texture tile set.
    /// </summary>
    /// <returns><see langword="true" /> when the package contains a .gts file.</returns>
    private bool DetectVirtualTexturePackage()
    {
        return _packageService.GetValidFiles()
            .Any(file => FileExtensions.IsVirtualTexture(Path.GetExtension(file.Name)));
    }
}
