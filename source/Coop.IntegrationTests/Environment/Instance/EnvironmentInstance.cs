using Common.Messaging;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.IntegrationTests.Environment.Mock;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment.Instance;

/// <summary>
/// Single instance of a server or client. Stores relevant test information.
/// </summary>
internal abstract class EnvironmentInstance
{
    public NetPeer NetPeer => mockNetwork.NetPeer;
    /// <summary>
    /// Messages sent internally or received over the network via the message broker
    /// </summary>
    public MessageCollection InternalMessages => messageBroker.Messages;
    /// <summary>
    /// Messages sent over the network
    /// </summary>
    public MessageCollection NetworkSentMessages => mockNetwork.NetworkSentMessages;

    private readonly TestMessageBroker messageBroker;
    private readonly MockNetworkBase mockNetwork;

    public EnvironmentInstance(IMessageBroker messageBroker, MockNetworkBase mockNetwork)
    {
        this.messageBroker = (TestMessageBroker)messageBroker;
        this.mockNetwork = mockNetwork;
    }

    /// <summary>
    /// Simulate receiving a message from the message broker
    /// </summary>
    /// <param name="source">Source of the message</param>
    /// <param name="message">Received Message</param>
    public void ReceiveMessage<T>(object source, T message) where T : IMessage
    {
        messageBroker.Publish(source, message);
    }

    /// <summary>
    /// Simulate receiving a packet from the network
    /// </summary>
    /// <param name="source">Source Peer</param>
    /// <param name="packet">Received Packet</param>
    public void ReceivePacket(NetPeer source, IPacket packet)
    {
        mockNetwork.ReceiveFromNetwork(source, packet);
    }
}