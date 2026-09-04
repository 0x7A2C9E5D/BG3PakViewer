using System.Windows;
using System.Windows.Controls;
using BG3PakViewer.Controls.ViewModels;

namespace BG3PakViewer.Controls.DataTemplateSelectors;

/// <summary>
///     Preview template selector
/// </summary>
internal class PreviewTemplateSelector : DataTemplateSelector
{
    /// <summary>
    ///     Image file preview template
    /// </summary>
    public DataTemplate? ImageFilePreviewTemplate { get; set; }

    /// <summary>
    ///     Model file preview template
    /// </summary>
    public DataTemplate? ModelFilePreviewTemplate { get; set; }

    /// <summary>
    ///     Plain text file preview template
    /// </summary>
    public DataTemplate? PlainTextFilePreviewTemplate { get; set; }

    /// <summary>
    ///     Not supported preview file data template
    /// </summary>
    public DataTemplate? NotSupportedPreviewFileDataTemplate { get; set; }

    /// <summary>
    ///     Story scripts preview template
    /// </summary>
    public DataTemplate? StoryScriptsPreviewTemplate { get; set; }

    /// <summary>
    ///     Larian resource preview template
    /// </summary>
    public DataTemplate? LarianResourcePreviewTemplate { get; set; }

    /// <summary>
    ///     Localization preview template
    /// </summary>
    public DataTemplate? LocalizationPreviewTemplate { get; set; }

    /// <summary>
    ///     Virtual texture preview template
    /// </summary>
    public DataTemplate? VirtualTexturePreviewTemplate { get; set; }

    /// <summary>
    ///     Select template
    /// </summary>
    /// <param name="item"></param>
    /// <param name="container"></param>
    /// <returns></returns>
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            ImagePreviewViewModel => ImageFilePreviewTemplate,
            Model3DPreviewViewModel => ModelFilePreviewTemplate,
            PlainTextPreviewViewModel => PlainTextFilePreviewTemplate,
            NotSupportedPreviewViewModel => NotSupportedPreviewFileDataTemplate,
            StoryScriptsPreviewViewModel => StoryScriptsPreviewTemplate,
            LarianResourcePreviewViewModel => LarianResourcePreviewTemplate,
            LocalizationPreviewViewModel => LocalizationPreviewTemplate,
            VirtualTexturePreviewViewModel => VirtualTexturePreviewTemplate,
            _ => null
        };
    }
}