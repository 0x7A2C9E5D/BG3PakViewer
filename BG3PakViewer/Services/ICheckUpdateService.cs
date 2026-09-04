namespace BG3PakViewer.Services;

/// <summary>
///     Check update service
/// </summary>
internal interface ICheckUpdateService
{
    /// <summary>
    ///     Check update
    /// </summary>
    /// <returns></returns>
    Task<bool> CheckUpdate();
}