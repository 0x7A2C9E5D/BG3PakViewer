using System.Runtime.InteropServices;
using BG3PakViewer.Dialogs.ViewModels;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.DependencyModel;
using Serilog;

namespace BG3PakViewer.ViewModels;

/// <summary>
///     Modal dialogs owned by the main window and the about data they display.
/// </summary>
internal partial class MainWindowViewModel
{
    /// <summary>
    ///     Show log dialog
    /// </summary>
    [RelayCommand]
    private Task ShowLogDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new LogDialogViewModel(_logAccessService, _shellOpenService),
            "Failed to show the log dialog.");
    }

    /// <summary>
    ///     Show recent dialog
    /// </summary>
    [RelayCommand]
    private Task ShowRecentDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new RecentDialogViewModel(_recentFilesService, _dialogService),
            "Failed to show the recent files dialog.");
    }

    /// <summary>
    ///     Show settings dialog
    /// </summary>
    [RelayCommand]
    private Task ShowSettingsDialog()
    {
        return ShowDialogWithLoggingAsync(
            () => new SettingsDialogViewModel(_settingsManagerService, _dialogService),
            "Failed to show the settings dialog.");
    }

    /// <summary>
    ///     Show about dialog
    /// </summary>
    [RelayCommand]
    private Task ShowAbout()
    {
        return ShowDialogWithLoggingAsync(
            () => new AboutDialogViewModel(BuildAboutData()),
            "Failed to show the about dialog.");
    }

    /// <summary>
    ///     Shows a dialog created by the given factory, disposing its view model when it supports
    ///     <see cref="IDisposable" />, and logs any failure with the given message.
    /// </summary>
    /// <typeparam name="TViewModel"></typeparam>
    /// <param name="viewModelFactory"></param>
    /// <param name="errorMessage"></param>
    /// <returns></returns>
    private async Task ShowDialogWithLoggingAsync<TViewModel>(
        Func<TViewModel> viewModelFactory,
        string errorMessage)
        where TViewModel : class, IModalDialogViewModel
    {
        TViewModel? viewModel = null;
        try
        {
            viewModel = viewModelFactory();
            await _dialogService.ShowDialogAsync(this, viewModel);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Message}", errorMessage);
        }
        finally
        {
            if (viewModel is IDisposable disposable) disposable.Dispose();
        }
    }

    /// <summary>
    ///     Build about data
    /// </summary>
    /// <returns></returns>
    private static Dictionary<string, object?> BuildAboutData()
    {
        return new Dictionary<string, object?>
        {
            { "Version", ThisAssembly.AssemblyInformationalVersion },
            { "BuildTime", BuildInformation.BuildAt.ToLocalTime() },
            { "OS", $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})" },
            { "Runtime", RuntimeInformation.FrameworkDescription },
            {
                "Package", DependencyContext.Default?
                    .RuntimeLibraries.Where(x => x.Type == "package")
                    .OrderBy(x => x.Name, StringComparer.InvariantCulture)
            }
        };
    }
}