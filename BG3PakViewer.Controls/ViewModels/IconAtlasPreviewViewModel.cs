using System.ComponentModel;
using BG3PakViewer.Contracts;
using BG3PakViewer.Shared.ViewModels;
using HanumanInstitute.MvvmDialogs;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     View model for previewing a sliced icon atlas as a grid of icons.
/// </summary>
/// <remarks>
///     The atlas is sliced by the loader, so this view model only holds the icons it is given; the size
///     of the cells comes from the sliced bitmaps themselves.
///     The icons are one flat list; how many of them share a line is decided by the wrap panel of the
///     view, which puts as many on a line as its width allows.
///     The class is split by responsibility: this file holds the icons and their selection, the
///     sibling files export them and talk to the user.
/// </remarks>
public partial class IconAtlasPreviewViewModel : DisposableViewModel
{
    private readonly IAppSettings _appSettings;
    private readonly string _atlasName;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="IconAtlasPreviewViewModel" /> class.
    /// </summary>
    /// <param name="atlasName">The name of the atlas, without its extension, used to name exported files.</param>
    /// <param name="items">The icons of the atlas, with the rectangles they were cut from.</param>
    /// <param name="exportService">The service the export commands hand the icons to.</param>
    /// <param name="dialogService">The service used to ask for the target folder and report the result.</param>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public IconAtlasPreviewViewModel(string atlasName,
        IEnumerable<IconAtlasCellViewModel> items,
        IPreviewExportService exportService,
        IDialogService dialogService,
        IAppSettings appSettings)
    {
        _exportService = exportService;
        _dialogService = dialogService;
        _appSettings = appSettings;
        _atlasName = atlasName;
        Items = items;

        // The selection is changed on the cells, so it has to be watched to keep the command state in
        // sync with the icons the user has picked.
        foreach (var item in Items)
            item.PropertyChanged += OnItemPropertyChanged;
    }

    /// <summary>
    ///     The icons of the atlas that hold something visible, in the order of the atlas.
    /// </summary>
    public IEnumerable<IconAtlasCellViewModel> Items { get; }

    /// <summary>
    ///     Whether at least one icon is selected, which is what the export command handles.
    /// </summary>
    private bool CanExportSelected()
    {
        return Items.Any(item => item.IsSelected);
    }

    /// <summary>
    ///     Keeps the export command state in sync with the selection of the icons.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IconAtlasCellViewModel.IsSelected))
            ExportSelectedCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    ///     Stops watching the icons, so a replaced preview no longer raises the export command state.
    /// </summary>
    /// <param name="disposing">Whether managed resources are released.</param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        foreach (var item in Items)
            item.PropertyChanged -= OnItemPropertyChanged;
    }
}
