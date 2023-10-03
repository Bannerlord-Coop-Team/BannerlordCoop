using Common.Messaging;
using Common.PacketHandlers;
using Coop.IntegrationTests.Environment.Mock;
using LiteNetLib;

namespace Coop.IntegrationTests.Environment.Instance;

/// <summary>
/// Single instance of a server or client. Stores relevant test information.
/// </summary>
internal abstract class EnvironmentInstance
{
    public NetPeer NetPeer => mockNetwork.NetPeer;
    public MessageCollection InternalMessages => messageBroker.Messages;
    public MessageCollection ExternalMessages => mockNetwork.NetworkSentMessages;

    private readonly TestMessageBroker messageBroker;
    private readonly MockNetworkBase mockNetwork;

    public EnvironmentInstance(IMessageBroker messageBroker, MockNetworkBase mockNetwork)
    {
        this.messageBroker = (TestMessageBroker)messageBroker;
        this.mockNetwork = mockNetwork;
    }

    public void SendMessage(object source, IMessage message)
    {
        messageBroker.Publish(source, message);
    }
    public void SendPacket(NetPeer source, IPacket packet)
    {
        mockNetwork.ReceiveFromNetwork(source, packet);
    }
}