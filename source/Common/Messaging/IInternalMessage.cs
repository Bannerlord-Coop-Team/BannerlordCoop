namespace Common.Messaging
{
    /// <summary>
    /// Internal message
    /// </summary>
    /// <remarks>
    /// An internal message is not meant to be sent
    /// over the network.
    /// 
    /// For network events <see cref="INetworkEvent"/>
    /// </remarks>
    public interface IInternalMessage
    {
    }
}