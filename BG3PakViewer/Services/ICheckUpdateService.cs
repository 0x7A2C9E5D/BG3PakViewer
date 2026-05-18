namespace BG3PakViewer.Services;

internal interface ICheckUpdateService
{
    public Task<bool> CheckUpdate();
}