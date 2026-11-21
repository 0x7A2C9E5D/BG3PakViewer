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
        return _state is { CanExport: true, HasSelection: true };
    }

    /// <summary>
    ///     Whether the search matches at least one goal, which is what exporting the search results
    ///     handles.
    /// </summary>
    /// <returns></returns>
    private bool CanExportSearchResults()
    {
        if (!_state.CanExport || _state.Search != StoryScriptsPreviewState.SearchPhase.Filtered) return false;
        var filter = ItemFilter;
        return filter is not null && Goals.Any(item => filter(item));
    }

    /// <summary>
    ///     Whether an export may start; the story always has goals to export.
    /// </summary>
    /// <returns></returns>
    private bool CanExportAll()
    {
        return _state.CanExport;
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
    [RelayCommand(CanExecute = nameof(CanExportAll))]
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
        _state.BeginFolderPick();
        string? folder = null;
        try
        {
            folder = await PickFolderAsync();
        }
        finally
        {
            if (folder is null) _state.CancelFolderPick();
        }

        if (folder is null)
        {
            Log.Information("Preview export folder selection was cancelled");
            return;
        }

        _state.BeginExport();
        try
        {
            var exported = 0;
            var failures = new List<PreviewExportResult>();
            await foreach (var result in _exportService.ExportAsync(folder, files, CancellationToken.None))
                if (result.Success)
                    exported++;
                else
                    failures.Add(result);

            await ReportExportResultAsync(exported, files.Count, failures, folder);
        }
        finally
        {
            _state.EndExport();
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
