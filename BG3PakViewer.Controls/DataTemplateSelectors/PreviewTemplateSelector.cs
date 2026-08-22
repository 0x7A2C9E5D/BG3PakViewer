using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Controls.ViewModels;

namespace BG3PakViewer.Controls.DataTemplateSelectors;

internal class PreviewTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FolderTemplate { get; set; }

    public DataTemplate? ImageFilePreviewTemplate { get; set; }

    public DataTemplate? ModelFilePreviewTemplate { get; set; }

    public DataTemplate? PlainTextFilePreviewTemplate { get; set; }

    public DataTemplate? NotSupportedPreviewFileDataTemplate { get; set; }

    public DataTemplate? OsirisScriptPreviewTemplate { get; set; }

    public DataTemplate? LarianResourcePreviewTemplate { get; set; }

    public DataTemplate? LocalizationPreviewTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            ImageFileViewModel => ImageFilePreviewTemplate,
            Model3DFileViewModel => ModelFilePreviewTemplate,
            PlainTextFilePreviewViewModel => PlainTextFilePreviewTemplate,
            NotSupportedFileViewModel => NotSupportedPreviewFileDataTemplate,
            OsirisScriptPreviewViewModel => OsirisScriptPreviewTemplate,
            LarianResourcePreviewViewModel => LarianResourcePreviewTemplate,
            LocalizationPreviewViewModel => LocalizationPreviewTemplate,
            _ => FolderTemplate
        };
    }
}