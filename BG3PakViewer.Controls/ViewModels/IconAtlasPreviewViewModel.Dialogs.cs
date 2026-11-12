using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     The dialogs of the atlas preview: asking for the target folder and reporting what was written.
/// </summary>
public partial class IconAtlasPreviewViewModel
{
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
}