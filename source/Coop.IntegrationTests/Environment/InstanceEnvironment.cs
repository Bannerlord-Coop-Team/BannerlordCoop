using Common.Messaging;
using Coop.IntegrationTests.Environment.Extensions;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment;

public abstract class InstanceEnvironment
{
    public TestMessageBroker MessageBroker;

    public MessageCollection InternalMessages => MessageBroker.Messages;

    public abstract NetPeer NetPeer { get; }

    public InstanceEnvironment(IMessageBroker messageBroker)
    {
        MessageBroker = (TestMessageBroker)messageBroker;
    }

    public void SendMessageInternal(object source, IMessage message)
    {
        MessageBroker.Publish(source, message);
    }
}

public class MessageCollection
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