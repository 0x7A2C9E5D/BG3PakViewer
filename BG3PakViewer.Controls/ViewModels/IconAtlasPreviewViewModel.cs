using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Syncfusion.Data.Extensions;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing a sliced icon atlas as a grid of icons.
/// </summary>
/// <remarks>
///     The atlas is sliced by the loader, so this view model only holds the icons it is given; the size
///     of the cells comes from the sliced bitmaps themselves.
///     The icons are one flat list; how many of them share a line is decided by the wrap panel of the
///     view, which puts as many on a line as its width allows.
/// </remarks>
public class IconAtlasPreviewViewModel : ObservableObject
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IconAtlasPreviewViewModel" /> class.
    /// </summary>
    /// <param name="icons"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public IconAtlasPreviewViewModel(IEnumerable<BitmapSource> icons)
    {
        ArgumentNullException.ThrowIfNull(icons);
        Items = icons.Select(x => new IconAtlasCellViewModel(x)).ToObservableCollection();
    }

    /// <summary>
    ///     The icons of the atlas that hold something visible, in the order of the atlas.
    /// </summary>
    public ObservableCollection<IconAtlasCellViewModel> Items { get; }
}