namespace Common.Messaging
{
    /// <summary>
    /// An event with a source ID and a property or field as a target. Event is managed by <see cref="IMessageBroker"/>
    /// </summary>
    /// <inheritdoc/>
    public interface ITargetEvent : IEvent
    {
        public string Id { get; }
    
        public string Target { get; }
    }
}
