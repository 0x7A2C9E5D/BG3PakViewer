using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;
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
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(exportService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(appSettings);
        ArgumentNullException.ThrowIfNull(atlasName);
        _exportService = exportService;
        _dialogService = dialogService;
        _appSettings = appSettings;
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
        return ExportAsync([.. Items.Where(item => item.IsSelected).Select(ToExportFile)]);
    }

    /// <summary>
    ///     Exports every icon of the atlas.
    /// </summary>
    [RelayCommand]
    private Task ExportAllAsync()
    {
        return ExportAsync([.. Items.Select(ToExportFile)]);
    }

    /// <summary>
    ///     Asks the user for a target folder and exports the files there.
    /// </summary>
    /// <param name="files">The files to export.</param>
    private async Task ExportAsync(IReadOnlyList<PreviewExportFile> files)
    {
        if (files.Count == 0) return;

        var folder = await PickFolderAsync();
        if (folder is null) return;

        var progress = new PreviewExportProgressReporter();
        await _exportService.ExportAsync(folder, files, progress);
        if (progress.Exported == files.Count)
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportCompleted, Strings.ExportCompleted,
                MessageBoxIcon.Success);
        else
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.ExportFailedMessage,
                Strings.ExportFailedCaption, MessageBoxIcon.Error);
    }

    /// <summary>
    ///     Asks the user where the files should be written.
    /// </summary>
    /// <returns>The chosen folder, or <see langword="null" /> when the user cancels.</returns>
    private async Task<string?> PickFolderAsync()
    {
        var storageFolder = await _dialogService.ShowOpenFolderDialogAsync(null, new OpenFolderDialogSettings
        {
            SuggestedStartLocation = Directory.Exists(_appSettings.DefaultExportDirectory)
                ? new DesktopDialogStorageFolder(_appSettings.DefaultExportDirectory)
                : null
        });
        if (storageFolder is not null) return storageFolder.LocalPath;
        Log.Information("Preview export folder selection was cancelled");
        return null;
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

    /// <summary>
    ///     Collects the progress reported by a preview export.
    /// </summary>
    private sealed class PreviewExportProgressReporter : IProgress<PreviewExportProgress<int>>
    {
        /// <summary>
        ///     Gets how many files have been written.
        /// </summary>
        public int Exported { get; private set; }

        /// <inheritdoc />
        public void Report(PreviewExportProgress<int> value)
        {
            Exported = value.Exported;
        }
    }
}