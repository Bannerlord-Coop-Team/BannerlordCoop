using Common.Messaging;
using System.Collections;

namespace Common.Tests.Utils;

/// <summary>
/// Collection of <see cref="IMessage"/>s
/// </summary>
public class MessageCollection : IEnumerable<IMessage>
{
    public readonly List<IMessage> Messages = new List<IMessage>();

    // Test-spawned background threads can send (append) while the test thread enumerates for an
    // assertion; an unguarded List race loses entries or tears the enumeration with no exception.
    private readonly object gate = new object();

    public int Count
    {
        get
        {
            lock (gate)
            {
                return Messages.Count;
            }
        }
    }

    /// <summary>
    /// Gets an iterator for all messages with type <typeparamref name="TMessage"/>
    /// </summary>
    /// <typeparam name="TMessage">Message type get iterator from</typeparam>
    /// <returns>Iterator for all messages with type <typeparamref name="TMessage"/></returns>
    public IEnumerable<TMessage> GetMessages<TMessage>() where TMessage : IMessage
    {
        return Snapshot()
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
    public void Add(IMessage message)
    {
        lock (gate)
        {
            Messages.Add(message);
        }
    }

    public IEnumerator<IMessage> GetEnumerator() => Snapshot().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Clear()
    {
        lock (gate)
        {
            Messages.Clear();
        }
    }

    private List<IMessage> Snapshot()
    {
        lock (gate)
        {
            return new List<IMessage>(Messages);
        }
    }
}
