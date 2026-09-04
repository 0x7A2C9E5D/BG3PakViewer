namespace BG3PakViewer.Contracts;

/// <summary>
///     Shell open service
/// </summary>
public interface IShellOpenService
{ 
    /// <summary>
    ///     Open file
    /// </summary>
    /// <param name="path"></param>
    void Open(string path);
}