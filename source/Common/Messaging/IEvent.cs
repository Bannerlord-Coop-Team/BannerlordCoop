namespace Common.Messaging
{
    /// <summary>
    /// Event that is managed by <see cref="IMessageBroker"/>
    /// </summary>
    /// <inheritdoc/>
    public interface IEvent : IInternalMessage
    {
    }
}
