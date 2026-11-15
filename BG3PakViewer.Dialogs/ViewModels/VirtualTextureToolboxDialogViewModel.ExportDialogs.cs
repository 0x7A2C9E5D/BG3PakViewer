using System.Globalization;
using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using BG3PakViewer.Locales;
using BG3PakViewer.Shared.Extensions;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;

namespace BG3PakViewer.Dialogs.ViewModels;

/// <summary>
///     The dialogs of an export run: choosing the target folder and telling the user what was written,
///     including the files that failed and the reason.
/// </summary>
public partial class VirtualTextureToolboxDialogViewModel
{
    /// <summary>
    ///     The completion message template, parsed once per dialog instead of per export.
    /// </summary>
    private readonly CompositeFormat _completedMessageFormat =
        CompositeFormat.Parse(Strings.PreviewExportCompletedMessage);

    /// <summary>
    ///     The partial completion message template, parsed once per dialog instead of per export.
    /// </summary>
    private readonly CompositeFormat _partialMessageFormat =
        CompositeFormat.Parse(Strings.PreviewExportPartialMessage);

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
    private async Task ReportExportResultAsync(int exported, int total, List<PreviewExportResult> failures)
    {
        if (failures.Count == 0)
        {
            await _dialogService.ShowMessageBoxNotifyAsync(this,
                string.Format(CultureInfo.CurrentCulture, _completedMessageFormat, exported),
                Strings.ExportCompleted, MessageBoxIcon.Success);
            return;
        }

        var message = new StringBuilder();
        message.AppendLine(exported == 0
            ? Strings.PreviewExportNothingMessage
            : string.Format(CultureInfo.CurrentCulture, _partialMessageFormat, exported, total));
        message.AppendLine().AppendLine(Strings.PreviewExportFailedFilesHeader);
        foreach (var failure in failures)
            message.AppendLine(CultureInfo.CurrentCulture, $"{failure.Name}: {failure.Reason}");

        await _dialogService.ShowMessageBoxNotifyAsync(this, message.ToString().TrimEnd(),
            exported == 0 ? Strings.ExportFailedCaption : Strings.PreviewExportPartiallyFailedCaption,
            MessageBoxIcon.Error);
    }
}
