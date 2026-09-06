using BG3PakViewer.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BG3PakViewer.Shared.ViewModels;

/// <summary>
///     Base for view models whose list is filtered from the global search box: it tracks the search
///     text, listens for <see cref="MessageTokens.SearchQueryChanged" />, and exposes the rebuilt
///     filter predicate for the view's collection filter behavior.
/// </summary>
public abstract partial class SearchFilterViewModel : DisposableViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SearchFilterViewModel" /> class.
    /// </summary>
    protected SearchFilterViewModel()
    {
        WeakReferenceMessenger.Default.Register<ValueChangedMessage<string?>, string>(
            this, MessageTokens.SearchQueryChanged, (_, message) => OnSearchMessage(message));
    }

    /// <summary>
    ///     The current search text.
    /// </summary>
    [ObservableProperty]
    // ReSharper disable once UnusedMember.Local
    private partial string? SearchText { get; set; }

    /// <summary>
    ///     Predicate the view applies to the item collection; null shows every item. Rebuilt whenever
    ///     <c>SearchText</c> changes, which the view picks up and re-applies.
    /// </summary>
    [ObservableProperty]
    public partial Predicate<object>? ItemFilter { get; private set; }

    /// <summary>
    ///     Handles search messages.
    /// </summary>
    /// <param name="message"></param>
    private void OnSearchMessage(ValueChangedMessage<string?> message)
    {
        SearchText = string.IsNullOrEmpty(message.Value) ? null : message.Value;
    }

    /// <summary>
    ///     Rebuilds the filter when the search text changes.
    /// </summary>
    /// <param name="value"></param>
    // ReSharper disable once UnusedParameterInPartialMethod
    partial void OnSearchTextChanged(string? value)
    {
        ItemFilter = BuildFilter(value);
    }

    /// <summary>
    ///     Builds the item filter for the given search text; null shows every item.
    /// </summary>
    /// <param name="searchText"></param>
    /// <returns></returns>
    protected abstract Predicate<object>? BuildFilter(string? searchText);

    /// <summary>
    ///     Unsubscribes from search messages.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        WeakReferenceMessenger.Default.Unregister<ValueChangedMessage<string?>, string>(this,
            MessageTokens.SearchQueryChanged);
        base.Dispose(disposing);
    }
}