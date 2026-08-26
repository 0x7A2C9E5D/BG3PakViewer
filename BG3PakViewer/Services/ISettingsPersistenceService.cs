namespace BG3PakViewer.Services;

internal interface ISettingsPersistenceService
{
    public void Save<T>(T settings);

    public T Load<T>() where T : new();
}