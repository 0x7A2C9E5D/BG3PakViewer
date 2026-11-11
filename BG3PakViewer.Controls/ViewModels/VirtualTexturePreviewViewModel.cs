using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using BG3PakViewer.Loader;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using BG3PakViewer.Shared.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using LSLib.VirtualTextures;
using Serilog;
using Image = SixLabors.ImageSharp.Image;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     virtual texture preview: lists textures on the left; selecting one extracts and decodes
///     the selected layer to a bitmap in the background, with cancellation and progress reporting.
/// </summary>
public partial class VirtualTexturePreviewViewModel : SearchFilterViewModel
{
    /// <summary>
    ///     Virtual texture layers are fixed: Albedo, Normal and Physical.
    /// </summary>
    private const int SupportedLayerCount = 3;

    /// <summary>
    ///     Layer names, kept in sync with the layer selector of the view; used to name exported files.
    /// </summary>
    private static readonly string[] LayerNames = ["Albedo", "Normal", "Physical"];

    private readonly IAppSettings _appSettings;
    private readonly IDialogService _dialogService;
    private readonly IPreviewExportService _exportService;
    private readonly VirtualTextureLoader _loader;
    private CancellationTokenSource? _cts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="VirtualTexturePreviewViewModel" /> class.
    /// </summary>
    /// <param name="loader"></param>
    /// <param name="dialogService">The service used to pick the export folder and report the result.</param>
    /// <param name="appSettings">The settings holding the default export directory.</param>
    /// <param name="exportService">The service that writes the exported layer files.</param>
    public VirtualTexturePreviewViewModel(VirtualTextureLoader loader, IDialogService dialogService,
        IAppSettings appSettings, IPreviewExportService exportService)
    {
        _loader = loader;
        _dialogService = dialogService;
        _appSettings = appSettings;
        _exportService = exportService;
        if (loader.LayerCount > SupportedLayerCount)
            throw new InvalidOperationException(
                $"Unsupported virtual texture: {loader.LayerCount} layers found, " +
                $"but at most {SupportedLayerCount} (Albedo, Normal, Physical) are supported.");
        foreach (var meta in loader.GetTextures())
            Textures.Add(new VirtualTextureItemViewModel(meta));
        SelectedTexture = Textures.FirstOrDefault();
    }

    /// <summary>
    ///     Full texture list. Filtering is a view concern: the view applies <c>ItemFilter</c>
    ///     to this collection's collection view, which keeps WPF's ICollectionView out of the view model.
    /// </summary>
    public ObservableCollection<VirtualTextureItemViewModel> Textures { get; } = [];

    /// <summary>
    ///     The currently selected texture.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial VirtualTextureItemViewModel? SelectedTexture { get; set; }

    /// <summary>
    ///     The index of the currently selected layer.
    /// </summary>
    [ObservableProperty]
    public partial int SelectedLayerIndex { get; set; }

    /// <summary>
    ///     The decoded preview image, platform-agnostic (no WPF types). The view converts it for
    ///     display; this view model owns it and disposes it when replaced or disposed.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial Image? Preview { get; private set; }

    /// <summary>
    ///     True if the view model is busy loading a preview.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>
    ///     The status text.
    /// </summary>
    [ObservableProperty]
    public partial string? StatusText { get; private set; }

    /// <summary>
    ///     The progress of the current operation, from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    ///     Builds the texture filter for the given search text.
    /// </summary>
    /// <param name="searchText"></param>
    /// <returns></returns>
    protected override Predicate<object>? BuildFilter(string? searchText)
    {
        return string.IsNullOrWhiteSpace(searchText)
            ? null
            : item => item is VirtualTextureItemViewModel texture &&
                      texture.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Loads the preview when the selected texture or layer changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedTextureChanged(VirtualTextureItemViewModel? value)
    {
        _ = LoadPreviewAsync();
    }

    /// <summary>
    ///     Loads the preview when the selected layer changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSelectedLayerIndexChanged(int value)
    {
        _ = LoadPreviewAsync();
    }

    /// <summary>
    ///     Loads the preview.
    /// </summary>
    private async Task LoadPreviewAsync()
    {
        var cts = await BeginLoadAsync();
        if (SelectedTexture is null) return;
        var meta = SelectedTexture.Meta;
        var layer = SelectedLayerIndex;

        try
        {
            await LoadPreviewCoreAsync(meta, layer, cts);
        }
        catch (OperationCanceledException)
        {
            // A new selection canceled the previous task; ignore
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to preview virtual texture: {Name}", meta.Name);
            Preview = null;
            StatusText = Strings.TitlePreviewFailed;
        }
        finally
        {
            if (!cts.IsCancellationRequested) IsBusy = false;
        }
    }

    /// <summary>
    ///     Loads the preview core.
    /// </summary>
    /// <param name="meta"></param>
    /// <param name="layer"></param>
    /// <param name="cts"></param>
    private async Task LoadPreviewCoreAsync(FourCCTextureMeta meta, int layer, CancellationTokenSource cts)
    {
        await using var ddsStream = await _loader.ExtractAsync(meta, layer,
            new Progress<double>(p => Progress = p), cts.Token);
        if (cts.IsCancellationRequested) return;

        if (ddsStream is null)
        {
            ShowNoData();
            return;
        }

        // Ownership of the image is transferred to Preview; it is disposed when replaced or disposed.
        // Decoding is CPU bound, so keep it off the UI thread the view model now lives on.
        var image = await Task.Run(() => ImageLoader.LoadAsync(ddsStream, ".dds"), cts.Token);
        if (cts.IsCancellationRequested)
        {
            image?.Dispose();
            return;
        }

        ShowPreview(image);
    }

    /// <summary>
    ///     Shows no data.
    /// </summary>
    private void ShowNoData()
    {
        Preview = null;
        StatusText = Strings.TitleNoDataForLayer;
    }

    /// <summary>
    ///     Begins a load operation.
    /// </summary>
    /// <returns></returns>
    private async Task<CancellationTokenSource> BeginLoadAsync()
    {
        if (_cts is not null)
            await _cts.CancelAsync();
        var cts = new CancellationTokenSource();
        _cts = cts;

        IsBusy = true;
        StatusText = Strings.TitleExtracting;
        Progress = 0;
        return cts;
    }

    /// <summary>
    ///     Shows the preview.
    /// </summary>
    /// <param name="image"></param>
    private void ShowPreview(Image? image)
    {
        Preview = image;
        StatusText = image is null
            ? Strings.TitleDecodeFailed
            : $"{image.Width} × {image.Height}";
    }

    /// <summary>
    ///     Whether there is a decoded layer that can be exported.
    /// </summary>
    private bool CanExport()
    {
        return SelectedTexture is not null && Preview is not null && !IsBusy;
    }

    /// <summary>
    ///     Exports every layer of the selected texture as one set of DDS files.
    ///     DDS is the only export format here: the raw layer data is written without re-encoding.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync()
    {
        var texture = SelectedTexture;
        if (texture is null) return Task.CompletedTask;

        var meta = texture.Meta;
        var name = Path.GetFileNameWithoutExtension(texture.DisplayName);
        var layerCount = Math.Min(_loader.LayerCount, LayerNames.Length);
        return layerCount == 0
            ? Task.CompletedTask
            : ExportFilesAsync([
                .. Enumerable.Range(0, layerCount)
                    .Select(layer => ToExportFile(name, meta, layer))
            ]);
    }

    /// <summary>
    ///     Asks the user for a target folder and exports the files there.
    /// </summary>
    /// <param name="files">The files to export.</param>
    private async Task ExportFilesAsync(IReadOnlyList<PreviewExportFile> files)
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

    /// <summary>
    ///     Turns one layer of the selected texture into the DDS file it is exported to.
    /// </summary>
    /// <param name="textureName">The texture name, without extension.</param>
    /// <param name="meta">The texture metadata.</param>
    /// <param name="layer">The layer index.</param>
    /// <returns>The file to write.</returns>
    private PreviewExportFile ToExportFile(string textureName, FourCCTextureMeta meta, int layer)
    {
        return new PreviewExportFile($"{textureName}_{LayerNames[layer]}.dds",
            () => OpenLayerAsync(meta, layer));
    }

    /// <summary>
    ///     Opens the raw DDS data of one layer. Ownership of the stream is transferred to the
    ///     export service, which copies it to the target file and disposes it afterward.
    /// </summary>
    /// <param name="meta"></param>
    /// <param name="layer"></param>
    private async Task<Stream> OpenLayerAsync(FourCCTextureMeta meta, int layer)
    {
        return await _loader.ExtractAsync(meta, layer, null, CancellationToken.None)
               ?? throw new InvalidDataException(Strings.TitleNoDataForLayer);
    }

    /// <summary>
    ///     Disposes the old preview when the new one is set.
    /// </summary>
    /// <param name="oldValue"></param>
    /// <param name="newValue"></param>
    partial void OnPreviewChanging(Image? oldValue, Image? newValue)
    {
        if (!ReferenceEquals(oldValue, newValue)) oldValue?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _ = _cts?.CancelAsync();
        _loader.Dispose();

        // Detach the view from the image before releasing it.
        Preview = null;
    }
}