using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using Serilog;

namespace BG3PakViewer.Services;

/// <summary>
///     Settings manager service
/// </summary>
internal class SettingsManagerService : ISettingsManagerService
{
    private readonly ICultureResolver _cultureResolver;

    /// <summary>
    ///     Settings manager service
    /// </summary>
    /// <param name="appSettings"></param>
    /// <param name="cultureResolver"></param>
    public SettingsManagerService(IAppSettings appSettings, ICultureResolver cultureResolver)
    {
        CurrentSettings = appSettings;
        _cultureResolver = cultureResolver;
        if (CurrentSettings is INotifyPropertyChanged notifyPropertyChanged)
            notifyPropertyChanged.PropertyChanged += OnAppSettingsPropertyChanged;
    }

    /// <summary>
    ///     Supported cultures
    /// </summary>
    public IEnumerable<CultureInfo> SupportedCultures => _cultureResolver.SupportedCultures;

    /// <summary>
    ///     Current settings
    /// </summary>
    public IAppSettings CurrentSettings { get; }

    /// <summary>
    ///     On app settings property changed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnAppSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAppSettings.Language):
                ApplyLanguageChange(CurrentSettings.Language);
                break;
        }
    }

    /// <summary>
    ///     Apply language change
    /// </summary>
    /// <param name="language"></param>
    private static void ApplyLanguageChange(string language)
    {
        try
        {
            I18NExtension.Culture = new CultureInfo(language);
            Log.Information("UI language applied: {Language}", language);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply language change: {Language}", language);
        }
    }
}