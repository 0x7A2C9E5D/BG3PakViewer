using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly IIconExportService _iconExportService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconAtlasPreviewViewModel" /> class.
    /// </summary>
    /// <param name="items">The icons of the atlas, with the rectangles they were cut from.</param>
    /// <param name="iconExportService">The service the export commands hand the icons to.</param>
    /// <param name="atlasName">The name of the atlas, without its extension, used to name exported files.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public IconAtlasPreviewViewModel(
        IEnumerable<IconAtlasCellViewModel> items,
        IIconExportService iconExportService,
        string atlasName)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(iconExportService);
        ArgumentNullException.ThrowIfNull(atlasName);
        _iconExportService = iconExportService;
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
        return _iconExportService.ExportAsync([.. Items.Where(item => item.IsSelected).Select(ToExportItem)]);
    }

    /// <summary>
    ///     Exports every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private Task ExportAllAsync()
    {
        return _iconExportService.ExportAsync([.. Items.Select(ToExportItem)]);
    }

    private IconExportItem ToExportItem(IconAtlasCellViewModel icon)
    {
        var fileName = icon.Row == 0
            ? $"{_atlasName}.png"
            : $"{_atlasName}_{icon.Row}_{icon.Column}.png";
        return new IconExportItem(icon.Image, fileName);
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