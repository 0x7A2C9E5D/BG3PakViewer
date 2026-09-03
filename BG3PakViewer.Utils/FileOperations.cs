using Serilog;

namespace BG3PakViewer.Utils;

public static class FileOperations
{
    public static async Task<bool> SaveStreamToFileAsync(Stream stream, string path)
    {
        try
        {
            await using var fs = File.Create(path);
            await stream.CopyToAsync(fs);
            await fs.FlushAsync();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save stream to file: {Path}", path);
            return false;
        }
    }

    public static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }
}