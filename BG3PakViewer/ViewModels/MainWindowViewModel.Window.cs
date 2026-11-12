using BG3PakViewer.Services;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Window level commands: what happens when the window is loaded and the external link it opens.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Window loaded async
    /// </summary>
    [RelayCommand]
    private async Task WindowLoadedAsync()
    {
        IsUpdateAvailable = await Ioc.Default.GetRequiredService<ICheckUpdateService>().CheckUpdate();
    }

    /// <summary>
    ///     Open nexus mods
    /// </summary>
    [RelayCommand]
    private void OpenNexusMods()
    {
        _shellOpenService.Open(AppSettings.NexusModUrl);
        Log.Information("NexusMods opened");
    }
}