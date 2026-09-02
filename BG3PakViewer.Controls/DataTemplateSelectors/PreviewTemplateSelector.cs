using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Controls.ViewModels;

namespace BG3PakViewer.Controls.DataTemplateSelectors;

internal class PreviewTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ImageFilePreviewTemplate { get; set; }

    public DataTemplate? ModelFilePreviewTemplate { get; set; }

    public DataTemplate? PlainTextFilePreviewTemplate { get; set; }

    public DataTemplate? NotSupportedPreviewFileDataTemplate { get; set; }

    public DataTemplate? OsirisScriptPreviewTemplate { get; set; }

    public DataTemplate? LarianResourcePreviewTemplate { get; set; }

    public DataTemplate? LocalizationPreviewTemplate { get; set; }

    public DataTemplate? GtsPreviewTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            ImagePreviewViewModel => ImageFilePreviewTemplate,
            Model3DPreviewViewModel => ModelFilePreviewTemplate,
            PlainTextPreviewViewModel => PlainTextFilePreviewTemplate,
            NotSupportedPreviewViewModel => NotSupportedPreviewFileDataTemplate,
            StoryScriptsPreviewViewModel => OsirisScriptPreviewTemplate,
            LarianResourcePreviewViewModel => LarianResourcePreviewTemplate,
            LocalizationPreviewViewModel => LocalizationPreviewTemplate,
            VirtualTexturePreviewViewModel => GtsPreviewTemplate,
            _ => null
        };
    }
}