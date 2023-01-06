namespace Common.Messaging
{
    /// <summary>
    /// Event that is managed by <see cref="IMessageBroker"/>
    /// </summary>
    /// <remarks>
    /// This should not be used over a network connection.
    /// For network events <see cref="INetworkEvent"/>
    /// </remarks>
    public interface IEvent
    {
    }
}
