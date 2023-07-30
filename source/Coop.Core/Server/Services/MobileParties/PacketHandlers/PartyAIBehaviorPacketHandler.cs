using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Packets;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.PacketHandlers;

/// <summary>
/// Handles incoming <see cref="RequestMobileMovementPacket"/>
/// </summary>
internal class RequestMobilePartyBehaviorPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.RequestMobilePartyMovement;

    private readonly IPacketManager packetManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;

    public RequestMobilePartyBehaviorPacketHandler(
        IPacketManager packetManager,
        INetwork network,
        IMessageBroker messageBroker)
    {
        this.packetManager = packetManager;
        this.network = network;
        this.messageBroker = messageBroker;
        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        RequestMobileMovementPacket convertedPacket = (RequestMobileMovementPacket)packet;

        messageBroker.Publish(this, new NetworkPartyMovementRequested(convertedPacket.MovementData));
    }
}
