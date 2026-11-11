using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
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
        _exportService = exportService;
        _dialogService = dialogService;
        _appSettings = appSettings;
        _atlasName = atlasName;
        Items = items.ToObservableCollection();

        // The selection is changed on the cells, so it has to be watched to keep the command state in
        // sync with the icons the user has picked.
        foreach (var item in Items)
            item.PropertyChanged += OnItemPropertyChanged;
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
        var folder = await PickFolderAsync();
        if (folder is not null)
        {
            var exported = 0;
            var failures = new List<PreviewExportResult>();
            await foreach (var result in _exportService.ExportAsync(folder, files))
                if (result.Success)
                    exported++;
                else
                    failures.Add(result);

            await ReportExportResultAsync(exported, files.Count, failures, folder);
        }
        else
        {
            Log.Information("Preview export folder selection was cancelled");
            await _dialogService.ShowMessageBoxNotifyAsync(this, Strings.PreviewExportCancelledMessage,
                Strings.CancelExportOperationCaption);
        }
    }

    /// <summary>
    ///     Tells the user what was written, and which files failed together with the reason.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="total">How many files the export attempted.</param>
    /// <param name="failures">The files that could not be written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportExportResultAsync(int exported, int total, IReadOnlyList<PreviewExportResult> failures,
        string folder)
    {
        if (failures.Count == 0)
            await ReportSuccessAsync(exported, folder);
        else
            await ReportFailuresAsync(exported, total, failures, folder);
    }

    /// <summary>
    ///     Tells the user that every file was written.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportSuccessAsync(int exported, string folder)
    {
        Log.Information("Exported {Exported} preview files to: {Folder}", exported, folder);
        await _dialogService.ShowMessageBoxNotifyAsync(this,
            string.Format(Strings.PreviewExportCompletedMessage, exported),
            Strings.ExportCompleted, MessageBoxIcon.Success);
    }

    /// <summary>
    ///     Tells the user how many files were written and which ones failed, together with the reason.
    /// </summary>
    /// <param name="exported">How many files were written.</param>
    /// <param name="total">How many files the export attempted.</param>
    /// <param name="failures">The files that could not be written.</param>
    /// <param name="folder">The folder the files were written to.</param>
    private async Task ReportFailuresAsync(int exported, int total, IReadOnlyList<PreviewExportResult> failures,
        string folder)
    {
        Log.Warning("Exported {Exported} of {Total} preview files to: {Folder}", exported, total, folder);

        var message = new StringBuilder();
        message.AppendLine(exported == 0
            ? Strings.PreviewExportNothingMessage
            : string.Format(Strings.PreviewExportPartialMessage, exported, total));
        message.AppendLine().AppendLine(Strings.PreviewExportFailedFilesHeader);
        foreach (var failure in failures)
            message.AppendLine($"{failure.Name}: {failure.Reason}");

        await _dialogService.ShowMessageBoxNotifyAsync(this, message.ToString().TrimEnd(),
            exported == 0 ? Strings.ExportFailedCaption : Strings.PreviewExportPartiallyFailedCaption,
            MessageBoxIcon.Error);
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
        return storageFolder?.LocalPath;
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