using Serilog;

namespace BG3PakViewer.Utils;

/// <summary>
///     FileOperations
/// </summary>
public static class FileOperations
{
    /// <summary>
    ///     Saves a stream to a file.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="path"></param>
    /// <returns></returns>
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

    /// <summary>
    ///     Ensures the directory exists.
    /// </summary>
    /// <param name="filePath"></param>
    public static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) 
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    ///     Resolves a path that package content names inside <paramref name="folder" /> and rejects it when
    ///     it would land outside that folder: a package entry or a tile set can name an absolute path or
    ///     use "." to write files the user never chose.
    /// </summary>
    /// <param name="folder">The folder the target must stay inside.</param>
    /// <param name="relativePath">The path the package content names.</param>
    /// <returns>The full path of the target file.</returns>
    /// <exception cref="InvalidDataException">The path would land outside <paramref name="folder" />.</exception>
    public static string ResolveInside(string folder, string relativePath)
    {
        var fullFolder = Path.GetFullPath(folder);
        var folderPrefix = Path.EndsInDirectorySeparator(fullFolder)
            ? fullFolder
            : fullFolder + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(fullFolder, relativePath));
        if (!fullPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"Path \"{relativePath}\" resolves outside the target folder \"{folder}\"");

        return fullPath;
    }
}