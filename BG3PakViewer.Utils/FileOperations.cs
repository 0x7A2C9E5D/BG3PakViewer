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
    /// <returns>True when the whole stream was written; false when the copy failed.</returns>
    public static async Task<bool> SaveStreamToFileAsync(Stream stream, string path)
    {
        // Write next to the target and rename it into place only after the copy succeeded: a failed
        // save must neither destroy the file that was already there nor leave a half-written one.
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await stream.CopyToAsync(file).ConfigureAwait(false);
                await file.FlushAsync().ConfigureAwait(false);
            }

            File.Move(tempPath, path, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save stream to file: {Path}", path);
            TryDelete(tempPath);
            return false;
        }
    }

    /// <summary>
    ///     Deletes a file a failed save left behind, without hiding the original failure.
    /// </summary>
    /// <param name="path">The file to remove, if it exists.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to remove the incomplete file: {Path}", path);
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
