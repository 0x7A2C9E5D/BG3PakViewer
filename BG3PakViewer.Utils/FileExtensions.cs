using System.Collections.Immutable;

namespace BG3PakViewer.Utils;

public static class FileExtensions
{
    private static readonly ImmutableHashSet<string> LarianBinaryResources =
        ImmutableHashSet.Create(".lsf", ".lsfx", ".lsb", ".lsbc", ".lsbs");

    private static readonly ImmutableHashSet<string> PlainTextFormats =
        ImmutableHashSet.Create(".lsx", ".lsj", ".xml", ".json", ".lua", ".txt");

    private static readonly ImmutableHashSet<string> LocalizationFormats = ImmutableHashSet.Create(".loca");

    private static readonly ImmutableHashSet<string> Model3DFormats = ImmutableHashSet.Create(".gr2", ".glb", ".gltf");

    private static readonly ImmutableHashSet<string> TextureFormats = ImmutableHashSet.Create(".dds");

    private static readonly ImmutableHashSet<string> BitmapImageFormats =
        ImmutableHashSet.Create(".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tiff");

    public static bool IsLarianBinaryResource(string extension)
    {
        return LarianBinaryResources.Contains(extension.ToLowerInvariant());
    }

    public static bool IsPlainText(string extension)
    {
        return PlainTextFormats.Contains(extension.ToLowerInvariant());
    }

    public static bool IsLocalizationFormat(string extension)
    {
        return LocalizationFormats.Contains(extension.ToLowerInvariant());
    }

    public static bool IsModel3DFormat(string extension)
    {
        return Model3DFormats.Contains(extension.ToLowerInvariant());
    }

    public static bool IsTextureFormat(string extension)
    {
        return TextureFormats.Contains(extension.ToLowerInvariant());
    }

    public static bool IsBitmapImage(string extension)
    {
        return BitmapImageFormats.Contains(extension.ToLowerInvariant());
    }

    public static bool IsLowTexTexture(string fileName)
    {
        return fileName.EndsWith("_lowtex.dds", StringComparison.OrdinalIgnoreCase);
    }
}