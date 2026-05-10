using System.ComponentModel;
using System.Globalization;
using System.Windows;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using Serilog;

namespace BG3PakViewer.Services;

internal class SettingsManagerService : ISettingsManagerService
{
    private readonly ICultureResolver _cultureResolver;

    public SettingsManagerService(IAppSettings appSettings, ICultureResolver cultureResolver)
    {
        CurrentSettings = appSettings;
        _cultureResolver = cultureResolver;
        if (CurrentSettings is INotifyPropertyChanged notifyPropertyChanged)
            notifyPropertyChanged.PropertyChanged += OnAppSettingsPropertyChanged;
    }

    public IEnumerable<CultureInfo> SupportedCultures => _cultureResolver.SupportedCultures;

    public IAppSettings CurrentSettings { get; }

    private void OnAppSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAppSettings.Language):
                ApplyLanguageChange(CurrentSettings.Language);
                break;
        }
    }

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