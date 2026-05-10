using CommunityToolkit.Mvvm.Messaging.Messages;

namespace BG3PakViewer.Messaging;

public class AsyncRequestMessage<TRequest, TResponse>(TRequest request) : AsyncRequestMessage<TResponse>
{
    public TRequest Request { get; } = request;
}