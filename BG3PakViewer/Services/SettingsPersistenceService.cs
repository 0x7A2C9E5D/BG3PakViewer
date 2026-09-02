using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BG3PakViewer.Contracts;
using BG3PakViewer.Miscellaneous;
using BG3PakViewer.Models;
using Serilog;

namespace BG3PakViewer.Services;

internal class SettingsPersistenceService : ISettingsPersistenceService
{
    private readonly string _filePath;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new InterfaceJsonConverter<IRecentFileEntry, RecentFileEntry>(),
            new ObservableCollectionJsonConverter<IRecentFileEntry>()
        }
    };

    public SettingsPersistenceService() : this(AppPaths.ConfigPath)
    {
    }

    public SettingsPersistenceService(string filePath)
    {
        _filePath = filePath;
        try
        {
            var configDirectory = Directory.GetParent(filePath)!.FullName;
            Directory.CreateDirectory(configDirectory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create the configuration directory for: {FilePath}", filePath);
        }
    }

    public void Save<T>(T settings)
    {
        try
        {
            using var stream = File.Create(_filePath);
            JsonSerializer.Serialize(stream, settings, _jsonSerializerOptions);
            Log.Debug("Settings saved to {FilePath}", _filePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {FilePath}", _filePath);
        }
    }

    public T Load<T>() where T : new()
    {
        if (!File.Exists(_filePath))
        {
            Log.Debug("No settings file at {FilePath}; using defaults.", _filePath);
            return new T();
        }

        try
        {
            var content = File.ReadAllText(_filePath);
            var result = JsonSerializer.Deserialize<T>(content, _jsonSerializerOptions);
            if (result is null)
            {
                Log.Warning("Settings file {FilePath} held no usable content; using defaults.", _filePath);
                return new T();
            }

            Log.Debug("Settings loaded from {FilePath}", _filePath);
            return result;
        }
        catch (Exception ex)
        {
            // A corrupted or unreadable settings file must not keep the application from starting.
            Log.Error(ex, "Failed to load settings from {FilePath}; using defaults.", _filePath);
            return new T();
        }
    }
}