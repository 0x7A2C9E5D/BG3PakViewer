using System.IO;
using BG3PakViewer.Controls.ViewModels;
using BG3PakViewer.Utils;
using LSLib.LS.Story;

namespace BG3PakViewer.Services.PreviewHandlers;

public class OsirisPreviewHandler : IPreviewHandler
{
    public bool CanHandle(string fileExtension)
    {
        return FileExtensions.IsOsirisScript(fileExtension);
    }

    public async Task<object?> CreatePreviewViewModelAsync(Stream stream, string fileExtension)
    {
        return await Task.Run(() =>
        {
            var reader = new StoryReader();
            var story = reader.Read(stream);
            return new OsirisScriptPreviewViewModel { Story = story };
        });
    }
}