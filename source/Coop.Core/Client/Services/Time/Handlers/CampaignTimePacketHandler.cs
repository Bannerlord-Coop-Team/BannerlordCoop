using Common.Messaging;
using Common.PacketHandlers;
using Coop.Core.Common.Network.Packets;
using Coop.Core.Server.Services.Time.Messages;
using LiteNetLib;

namespace Coop.Core.Client.Services.Time.Handlers;

/// <summary>
/// Receives the server's <see cref="CampaignTimePacket"/> heartbeat and republishes it locally as
/// <see cref="CampaignTimeUpdated"/>, so the existing pacing pipeline
/// (<see cref="CampaignTimeSyncHandler"/>) is fed identically whether the heartbeat arrived as a
/// packet or, historically, as a message.
/// </summary>
public class CampaignTimePacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.CampaignTime;

    private readonly IPacketManager packetManager;
    private readonly IMessageBroker messageBroker;

    public CampaignTimePacketHandler(IPacketManager packetManager, IMessageBroker messageBroker)
    {
        this.packetManager = packetManager;
        this.messageBroker = messageBroker;
        this.packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        var timePacket = (CampaignTimePacket)packet;
        messageBroker.Publish(this, new CampaignTimeUpdated(timePacket.ServerTicks));
    }
}
