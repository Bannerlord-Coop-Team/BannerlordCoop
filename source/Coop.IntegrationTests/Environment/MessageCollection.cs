using Common.Messaging;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Collection of <see cref="IMessage"/>s
/// </summary>
internal class MessageCollection
{
    public readonly List<IMessage> Messages = new List<IMessage>();

    public int Count => Messages.Count;

    public IEnumerable<TMessage> GetMessages<TMessage>() where TMessage : IMessage
    {
        return Messages
            .Where(msg => typeof(TMessage).IsAssignableFrom(msg.GetType()))
            .Select(msg => (TMessage)msg);
    }

    public int GetMessageCount<TMessage>() where TMessage : IMessage
    {
        return GetMessages<TMessage>().Count();
    }

    public void Add(IMessage message) => Messages.Add(message);
}