using System.Collections.Immutable;

namespace BG3PakViewer.Utils;

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

    public static bool IsLarianResource(string extension)
    {
        return LarianResourceFormats.Contains(extension.ToLowerInvariant());
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

    public static bool IsStoryScripts(string extension)
    {
        return extension.Equals(".osi", StringComparison.OrdinalIgnoreCase);
    }
}