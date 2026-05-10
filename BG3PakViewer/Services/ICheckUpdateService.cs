namespace BG3PakViewer.Services;

public interface ICheckUpdateService
{
    public Task<bool> CheckUpdate();
}