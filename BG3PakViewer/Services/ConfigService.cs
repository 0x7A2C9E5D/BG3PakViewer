using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using BG3PakViewer.Contracts;
using BG3PakViewer.Miscellaneous;
using BG3PakViewer.Models;

namespace BG3PakViewer.Services;

internal class ConfigService : IConfigService
{
    private readonly string _filePath;

    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter(),
            new InterfaceJsonConverter<IRecentItem, RecentItem>(),
            new ObservableCollectionJsonConverter<IRecentItem>()
        }
    };

    public ConfigService() : this(AppPaths.ConfigPath)
    {
    }

    public ConfigService(string filePath)
    {
        _filePath = filePath;
        var configDirectory = Directory.GetParent(filePath)!.FullName;
        Directory.CreateDirectory(configDirectory);
    }

    public void Save<T>(T settings)
    {
        using var stream = File.Create(_filePath);
        JsonSerializer.Serialize(stream, settings, _jsonSerializerOptions);
    }

    public T Load<T>() where T : new()
    {
        if (!File.Exists(_filePath))
            return new T();

        var content = File.ReadAllText(_filePath);
        var result = JsonSerializer.Deserialize<T>(content, _jsonSerializerOptions);
        return result ?? new T();
    }
}