using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Extensions;
using BG3PakViewer.Utils;
using Cyotek.Drawing.BitmapFont;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Size = System.Drawing.Size;

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
        using var textureImage = await LoadTexture(pngStream);
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

    private static Image<Rgba32> RenderPreview(BitmapFont font, Image<Rgba32> texture, string text, Size size)
    {
        var position = new Point(0, 0);
        var previousCharacter = ' ';
        var previewImage = new Image<Rgba32>(size.Width, size.Height, new Rgba32(0, 0, 0, 0));
        foreach (var character in text)
            HandleCharacter(font, texture, previewImage, character, ref position, ref previousCharacter);
        return previewImage;
    }

    private static void HandleCharacter(
        BitmapFont font,
        Image<Rgba32> texture,
        Image<Rgba32> target,
        char character,
        ref Point position,
        ref char previousCharacter)
    {
        switch (character)
        {
            case '\n':
                position = new Point(0, position.Y + font.LineHeight);
                break;
            case '\r':
                break;
            default:
                var data = font[character];
                if (data.IsEmpty) break;

                var kerning = font.GetKerning(previousCharacter, character);
                var drawPosition = new Point(
                    position.X + data.XOffset + kerning,
                    position.Y + data.YOffset);
                DrawCharacter(target, texture, data, drawPosition);
                previousCharacter = character;
                position = new Point(position.X + data.XAdvance + kerning, position.Y);
                break;
        }
    }

    private static void DrawCharacter(Image<Rgba32> target, Image<Rgba32> texture, Character character, Point position)
    {
        var rectangle = new Rectangle(character.X, character.Y, character.Width, character.Height);
        target.Mutate(ctx =>
        {
            ctx.DrawImage(
                texture.Clone(crop => crop.Crop(rectangle)),
                position,
                1f);
        });
    }
}