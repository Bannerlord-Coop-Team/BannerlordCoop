using Common.Messaging;

namespace Coop.IntegrationTests.Environment;

/// <summary>
/// Collection of <see cref="IMessage"/>s
/// </summary>
internal class MessageCollection
{
    public readonly List<IMessage> Messages = new List<IMessage>();

    public int Count => Messages.Count;

    /// <summary>
    /// Gets an iterator for all messages with type <typeparamref name="TMessage"/>
    /// </summary>
    /// <typeparam name="TMessage">Message type get iterator from</typeparam>
    /// <returns>Iterator for all messages with type <typeparamref name="TMessage"/></returns>
    public IEnumerable<TMessage> GetMessages<TMessage>() where TMessage : IMessage
    {
        return Messages
            .Where(msg => typeof(TMessage).IsAssignableFrom(msg.GetType()))
            .Select(msg => (TMessage)msg);
    }

    /// <summary>
    /// Gets the total number of messages with type <typeparamref name="TMessage"/>
    /// </summary>
    /// <typeparam name="TMessage">Type of message to get count</typeparam>
    /// <returns>Number of messages with type <typeparamref name="TMessage"/> exist</returns>
    public int GetMessageCount<TMessage>() where TMessage : IMessage
    {
        return GetMessages<TMessage>().Count();
    }

    /// <summary>
    /// Adds a message to the collection
    /// </summary>
    /// <param name="message">Message to add to the collection</param>
    public void Add(IMessage message) => Messages.Add(message);
}