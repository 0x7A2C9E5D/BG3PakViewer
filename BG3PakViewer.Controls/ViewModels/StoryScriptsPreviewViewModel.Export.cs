using System.IO;
using System.Text;
using BG3PakViewer.Contracts;
using CommunityToolkit.Mvvm.Input;
using LSLib.LS.Story;
using Serilog;

namespace BG3PakViewer.Controls.ViewModels;

/// <summary>
///     Exporting the decompiled scripts of the story: the script of the selected goal, the scripts
///     of the goals that match the current search, or the script of every goal at once.
/// </summary>
public partial class StoryScriptsPreviewViewModel
{
    /// <summary>
    ///     Whether a goal is selected, which is what exporting the current script handles.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSelected()
    {
        return SelectedGoal is not null;
    }

    /// <summary>
    ///     Whether the search matches at least one goal, which is what exporting the search results
    ///     handles.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSearchResults()
    {
        return ItemFilter is { } filter && Goals.Any(item => filter(item));
    }

    /// <summary>
    ///     Exports the decompiled script of the selected goal.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSelected))]
    private Task ExportSelectedAsync()
    {
        var goal = SelectedGoal;
        return goal is null ? Task.CompletedTask : ExportAsync([ToExportFile(goal)]);
    }

    /// <summary>
    ///     Exports the decompiled scripts of the goals that match the current search.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExportSearchResults))]
    private Task ExportSearchResultsAsync()
    {
        var filter = ItemFilter;
        return filter is null
            ? Task.CompletedTask
            : ExportAsync([.. Goals.Where(item => filter(item)).Select(ToExportFile)]);
    }

    /// <summary>
    ///     Exports the decompiled script of every goal of the story.
    /// </summary>
    [RelayCommand]
    private Task ExportAllAsync()
    {
        return ExportAsync([.. Goals.Select(ToExportFile)]);
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
        }
    }

    /// <summary>
    ///     Turns a goal into the text file its decompiled script is exported to, named after the goal.
    /// </summary>
    /// <param name="item">The goal to export.</param>
    /// <returns>The file to write.</returns>
    private static PreviewExportFile ToExportFile(StoryScriptsGoalItemViewModel item)
    {
        return new PreviewExportFile($"{item.Name}.txt",
            () => Task.Run(Stream () => EncodeScript(item.Goal!)));
    }

    /// <summary>
    ///     Decompiles one goal and returns its script as a UTF-8 stream. The script is decompiled
    ///     in full: unlike the preview, the export is not truncated to the configured line limit.
    /// </summary>
    /// <param name="goal">The goal to decompile.</param>
    /// <returns>The script text, positioned at its start.</returns>
    private static MemoryStream EncodeScript(Goal goal)
    {
        using var writer = new StringWriter();
        goal.MakeScript(writer, goal.Story);
        return new MemoryStream(Encoding.UTF8.GetBytes(writer.ToString()));
    }
}