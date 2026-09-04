using System.Collections.Immutable;

namespace BG3PakViewer.Utils;

/// <summary>
///     FileExtensions
/// </summary>
public static class FileExtensions
{
    private static readonly ImmutableHashSet<string> LarianResourceFormats =
        ImmutableHashSet.Create(".lsf", ".lsfx", ".lsb", ".lsbc", ".lsbs", ".lsx", ".lsj");

    private static readonly ImmutableHashSet<string> PlainTextFormats =
        ImmutableHashSet.Create(".xml", ".json", ".lua", ".txt", ".xaml", ".ann", ".anc", ".khn");

    private static readonly ImmutableHashSet<string> LocalizationFormats = ImmutableHashSet.Create(".loca");

    private static readonly ImmutableHashSet<string> Model3DFormats = ImmutableHashSet.Create(".gr2", ".glb", ".gltf");

    private static readonly ImmutableHashSet<string> TextureFormats = ImmutableHashSet.Create(".dds");

    private static readonly ImmutableHashSet<string> BitmapImageFormats =
        ImmutableHashSet.Create(".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff", ".tga", ".cur");

    /// <summary>
    ///     Determines whether the specified extension is a Larian resource.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsLarianResource(string extension)
    {
        return LarianResourceFormats.Contains(extension.ToLowerInvariant());
    }
    
    /// <summary>
    ///     Determines whether the specified extension is a plain text.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsPlainText(string extension)
    {
        return PlainTextFormats.Contains(extension.ToLowerInvariant());
    }
    
    /// <summary>
    ///     Determines whether the specified extension is a localization.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsLocalizationFormat(string extension)
    {
        return LocalizationFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    ///     Determines whether the specified extension is a model 3D format.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsModel3DFormat(string extension)
    {
        return Model3DFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    ///     Determines whether the specified extension is a texture format.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsTextureFormat(string extension)
    {
        return TextureFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    ///     Determines whether the specified extension is a bitmap image format.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsBitmapImage(string extension)
    {
        return BitmapImageFormats.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    ///     Determines whether the specified extension is a Vorbis audio format.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsVorbisAudio(string extension)
    {
        return extension.Equals(".ogg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether the specified extension is a Wwise audio format.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsWwiseAudio(string extension)
    {
        return extension.Equals(".wem", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether the specified file name is a low tex texture.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static bool IsLowTexTexture(string fileName)
    {
        return fileName.EndsWith("_lowtex.dds", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether the specified extension is a story scripts.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsStoryScripts(string extension)
    {
        return extension.Equals(".osi", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Determines whether the specified extension is a virtual texture.
    /// </summary>
    /// <param name="extension"></param>
    /// <returns></returns>
    public static bool IsVirtualTexture(string extension)
    {
        return extension.Equals(".gts", StringComparison.OrdinalIgnoreCase);
    }
}