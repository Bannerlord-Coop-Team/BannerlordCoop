using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace Coop.Core.Common.Network.Packets;

/// <summary>
/// The server's campaign-clock heartbeat: the authoritative <c>MapTimeTracker</c> tick value,
/// broadcast on a fixed real-time interval.
/// </summary>
/// <remarks>
/// Sent <see cref="DeliveryMethod.Sequenced"/> — unreliable, newest-wins — rather than as a reliable
/// message. The heartbeat is monotonic state where only the latest value matters, so a lost one is
/// simply replaced by the next; in exchange it can never be head-of-line blocked behind a congested
/// world-sync stream, and it adds nothing to the reliable queue depth that triggers the server's
/// catch-up pause. That keeps client clocks live under exactly the load that used to freeze them.
/// </remarks>
[ProtoContract(SkipConstructor = true)]
public readonly struct CampaignTimePacket : IPacket
{
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Sequenced;

    public PacketType PacketType => PacketType.CampaignTime;

    [ProtoMember(1)]
    public readonly long ServerTicks;

    /// <summary>Join backlog, or a negative value when this peer is not joining.</summary>
    [ProtoMember(2)]
    public readonly int JoinPacketsRemaining;

    public CampaignTimePacket(long serverTicks, int joinPacketsRemaining)
    {
        ServerTicks = serverTicks;
        JoinPacketsRemaining = joinPacketsRemaining;
    }
}
