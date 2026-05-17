using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Utils;

namespace BG3PakViewer.Services.PreviewHandlers;

public class FontPreviewHandler : IMultiStreamPreviewHandler
{
    public bool CanHandle(string extension)
    {
        return FileExtensions.IsFontFormat(extension);
    }

    public IEnumerable<string> GetRelatedFilePatterns(string primaryFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(primaryFilePath);
        var directory = Path.GetDirectoryName(primaryFilePath);
        
        // 返回需要的关联文件路径模式
        yield return $"{directory}/{baseName}_0.png";
        yield return $"{directory}/{baseName}_1.png";
    }

    public async Task<object?> CreatePreviewViewModelAsync(Dictionary<string, Stream> streams)
    {
        if (!streams.TryGetValue(".fnt", out var fntStream))
            return null;

        string fntContent;
        using (var reader = new StreamReader(fntStream))
        {
            fntContent = await reader.ReadToEndAsync();
        }

        var textureInfo = new List<string>();
        foreach (var kvp in streams.Where(k => k.Key != ".fnt"))
        {
            using var bitmap = new System.Drawing.Bitmap(kvp.Value);
            textureInfo.Add($"{kvp.Key}: {bitmap.Width}x{bitmap.Height}");
        }

        return new PlainTextFilePreviewViewModel
        {
            Data = $"[字体定义]\n{fntContent}\n\n[字体纹理]\n{string.Join("\n", textureInfo)}"
        };
    }
}