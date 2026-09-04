using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BG3PakViewer.Messaging;

/// <summary>
///     AsyncRequestMessage
/// </summary>
/// <param name="request"></param>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class AsyncRequestMessage<TRequest, TResponse>(TRequest request) : AsyncRequestMessage<TResponse>
{
    /// <summary>
    ///     Gets the request.
    /// </summary>
    public TRequest Request { get; } = request;
}