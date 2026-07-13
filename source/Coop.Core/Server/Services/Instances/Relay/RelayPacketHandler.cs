using Common.Logging;
using Common.Network;
using Common.PacketHandlers;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.Instances.Relay;

/// <summary>
/// Server-side relay fallback: forwards a <see cref="RelayPacket"/>'s payload to the live connections of
/// the controllers it names, resolved per instance by the <see cref="IMissionManager"/>. The payload is
/// relayed verbatim — the server never deserializes it.
/// </summary>
internal class RelayPacketHandler : IPacketHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<RelayPacketHandler>();
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

        if (!missionManager.TryGetRelayTarget(relayPacket.InstanceId, relayPacket.ControllerId, out var target))
        {
            // Expected briefly around departures: the target is removed from the instance the moment its
            // leave/disconnect is processed, while senders keep relaying at it until the departure fan-out
            // reaches them. Sustained misses for one controller mean its membership diverged (see
            // MissionContext.BeginInstance/EndInstance).
            Logger.Warning("Failed to get peer for instance ({InstanceId}) controller ({ControllerId})", relayPacket.InstanceId, relayPacket.ControllerId);
            return;
        }

        target.Send(relayPacket.Payload, relayPacket.DeliveryMethod);
    }
}
