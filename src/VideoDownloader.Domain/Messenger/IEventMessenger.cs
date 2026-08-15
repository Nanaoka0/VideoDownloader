namespace VideoDownloader.Domain.Messenger;

public interface IEventMessenger
{
    void Send<TMessage>(TMessage message) where TMessage : class;
    IDisposable Subscribe<TMessage>(Action<TMessage> handler) where TMessage : class;
}