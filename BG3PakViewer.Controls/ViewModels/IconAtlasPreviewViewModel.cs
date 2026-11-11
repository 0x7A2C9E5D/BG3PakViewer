using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
public partial class IconAtlasPreviewViewModel : ObservableObject
{
    private readonly string _atlasName;
    private readonly IPreviewExportService _exportService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconAtlasPreviewViewModel" /> class.
    /// </summary>
    /// <param name="items">The icons of the atlas, with the rectangles they were cut from.</param>
    /// <param name="exportService">The service the export commands hand the icons to.</param>
    /// <param name="atlasName">The name of the atlas, without its extension, used to name exported files.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public IconAtlasPreviewViewModel(
        IEnumerable<IconAtlasCellViewModel> items,
        IPreviewExportService exportService,
        string atlasName)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(atlasName);
        _exportService = exportService;
        _atlasName = atlasName;
        Items = items.ToObservableCollection();

        // The selection is changed on the cells, so it has to be watched to keep the command state in
        // sync with the icons the user has picked.
        foreach (var item in Items) item.PropertyChanged += OnItemPropertyChanged;
    }

    /// <summary>
    ///     The icons of the atlas that hold something visible, in the order of the atlas.
    /// </summary>
    public ObservableCollection<IconAtlasCellViewModel> Items { get; }

    /// <summary>
    ///     Exports the icons the user has selected.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private Task ExportSelectedAsync()
    {
        return _exportService.ExportAsync([.. Items.Where(item => item.IsSelected).Select(ToExportFile)]);
    }

    /// <summary>
    ///     Exports every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private Task ExportAllAsync()
    {
        return _exportService.ExportAsync([.. Items.Select(ToExportFile)]);
    }

    private PreviewExportFile ToExportFile(IconAtlasCellViewModel icon)
    {
        var fileName = icon.Row == 0
            ? $"{_atlasName}.png"
            : $"{_atlasName}_{icon.Row}_{icon.Column}.png";
        return new PreviewExportFile(fileName, () => Task.Run(Stream () => EncodePng(icon.Image)));
    }

    private static MemoryStream EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
    }

    private bool CanExportSelected()
    {
        return Items.Any(item => item.IsSelected);
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IconAtlasCellViewModel.IsSelected))
            ExportSelectedCommand.NotifyCanExecuteChanged();
    }
}