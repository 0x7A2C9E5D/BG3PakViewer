namespace BG3PakViewer.Services;

/// <summary>
///     Settings persistence service
/// </summary>
internal interface ISettingsPersistenceService
{
    /// <summary>
    ///     Save settings
    /// </summary>
    /// <param name="settings"></param>
    /// <typeparam name="T"></typeparam>
    void Save<T>(T settings);

    /// <summary>
    ///     Load settings
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T Load<T>() where T : new();
}