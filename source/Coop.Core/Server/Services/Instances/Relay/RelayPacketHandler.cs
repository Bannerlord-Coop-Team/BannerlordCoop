using Common.PacketHandlers;
using LiteNetLib;

namespace Coop.Core.Server.Services.Instances.Relay;

/// <summary>
/// Server-side relay fallback: forwards a <see cref="RelayPacket"/>'s payload to the live connections of
/// the controllers it names, resolved per instance by the <see cref="IMissionManager"/>. The payload is
/// relayed verbatim — the server never deserializes it.
/// </summary>
internal class RelayPacketHandler : IPacketHandler
{
    private readonly IPacketManager packetManager;
    private readonly IMissionManager missionManager;

    public PacketType PacketType => PacketType.Relay;

    public RelayPacketHandler(IPacketManager packetManager, IMissionManager missionManager)
    {
        this.packetManager = packetManager;
        this.missionManager = missionManager;

        packetManager.RegisterPacketHandler(this);
    }

    public void Dispose()
    {
        packetManager.RemovePacketHandler(this);
    }

    public void HandlePacket(NetPeer peer, IPacket packet)
    {
        RelayPacket relayPacket = (RelayPacket)packet;

        foreach (var target in missionManager.GetRelayTargets(relayPacket.InstanceId, relayPacket.ControllerIds))
        {
            target.Send(relayPacket.Payload, relayPacket.DeliveryMethod);
        }
    }
}
