using Common.Messaging;
using Common.Network;
using Common.PacketHandlers;
using Coop.Core.Client.Services.MobileParties.Packets;
using Coop.Core.Server.Services.MobileParties.Packets;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.PacketHandlers;

/// <summary>
/// Handles incoming <see cref="RequestMobilePartyBehaviorPacket"/>
/// </summary>
internal class RequestMobilePartyBehaviorPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.RequestMobilePartyBehavior;

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
        RequestMobilePartyBehaviorPacket convertedPacket = (RequestMobilePartyBehaviorPacket)packet;

        var data = convertedPacket.BehaviorUpdateData;

        network.SendAll(new UpdatePartyBehaviorPacket(data));

        messageBroker.Publish(this, new UpdatePartyBehavior(data));
    }
}
