namespace Coop.Communication.MessageBroker
{
    public enum MessageScope
    {
        Invalid,
        
        /// <summary>
        /// Message is only broadcast internally (not across the network)
        /// </summary>
        Internal,
        
        /// <summary>
        /// Message is only broadcast across the network (not internally)
        /// </summary>
        External
    }
}