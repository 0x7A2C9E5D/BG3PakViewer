using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Extensions;
using BG3PakViewer.Utils;
using Cyotek.Drawing.BitmapFont;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BG3PakViewer.Services.PreviewHandlers;

public class BitmapFontPreviewHandler : IMultiStreamPreviewHandler
{
    public bool CanHandle(string extension)
    {
        return FileExtensions.IsFontFormat(extension);
    }

    public IEnumerable<string> GetRelatedFilePatterns(string primaryFilePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(primaryFilePath);
        var directory = Path.GetDirectoryName(primaryFilePath);
        yield return $"{directory}/{baseName}_0.png";
    }

    public async Task<object?> CreatePreviewViewModelAsync(Dictionary<string, Stream> streams)
    {
        if (streams.Count != 2) return null;
        if (!streams.TryGetValue(".fnt", out var fntStream) 
            || !streams.TryGetValue(".png", out var pngStream)) return null;
        var font = LoadFont(fntStream);
        var textureImage = await LoadTexture(pngStream);
        const string previewText = """
                                   ABCDEFGHIJKLMNOPQRSTUVWXYZ
                                   abcdefghijklmnopqrstuvwxyz
                                   0123456789
                                   !@#$%^&*()_+-=[]{}|;':",./<>?
                                   """;
        var size = font.MeasureFont(previewText);
        if (size.Width == 0 || size.Height == 0) return null;
        using var previewImage = RenderPreview(font, textureImage, previewText, size);
        return new ImageFileViewModel { Data = previewImage.ToBitmapSource() };
    }

    private static BitmapFont LoadFont(Stream stream)
    {
        var font = new BitmapFont();
        font.LoadBinary(stream);
        return font;
    }

    private static async Task<Image<Rgba32>> LoadTexture(Stream stream)
    {
        return await Image.LoadAsync<Rgba32>(stream);
    }

    private static Image<Rgba32> RenderPreview(BitmapFont font, Image<Rgba32> texture, string text,System.Drawing.Size size)
    {
        var x = 0;
        var y = 0;
        var previousCharacter = ' ';
        var previewImage = new Image<Rgba32>(size.Width, size.Height, new Rgba32(0, 0, 0, 0));
        foreach (var character in text)
            (x, y) = HandleCharacter(font, texture, previewImage, character, x, y, ref previousCharacter);
        return previewImage;
    }

    private static (int x, int y) HandleCharacter(
        BitmapFont font,
        Image<Rgba32> texture,
        Image<Rgba32> target,
        char character,
        int x,
        int y,
        ref char previousCharacter)
    {
        switch (character)
        {
            case '\n':
                return (0, y + font.LineHeight);

            case '\r':
                return (x, y);

            default:
                var data = font[character];
                if (data.IsEmpty) return (x, y);
                var kerning = font.GetKerning(previousCharacter, character);
                DrawCharacter(target, texture, data, x + data.XOffset + kerning, y + data.YOffset);
                previousCharacter = character;
                return (x + data.XAdvance + kerning, y);
        }
    }

    private static void DrawCharacter(Image<Rgba32> target, Image<Rgba32> texture, Character character, int x, int y)
    {
        var sourceRectangle = new Rectangle(character.X, character.Y, character.Width, character.Height);

        target.Mutate(ctx =>
        {
            ctx.DrawImage(
                texture.Clone(crop => crop.Crop(sourceRectangle)),
                new Point(x, y),
                1f);
        });
    }
}