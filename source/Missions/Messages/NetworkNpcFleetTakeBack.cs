using Common.Messaging;
using ProtoBuf;
using System;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>
/// To peers over the mission mesh: the battle's failed AI fleet attempt for <see cref="Side"/> is still being taken back by
/// <see cref="Holder"/>, so no client fields the fleet, and <see cref="ShipIds"/> are the hulls left for it to take back.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkNpcFleetTakeBack : IEvent
{
    [ProtoMember(1)] public readonly BattleSideEnum Side;
    [ProtoMember(2)] public readonly string Holder;
    [ProtoMember(3)] private readonly Guid[] shipIds;
    /// <summary>The host change the hulls moved at; the holder takes them back once it applied that change.</summary>
    [ProtoMember(4)] public readonly int HostEpoch;

    public NetworkNpcFleetTakeBack(BattleSideEnum side, string holder, Guid[] shipIds, int hostEpoch)
    {
        Side = side;
        Holder = holder;
        this.shipIds = shipIds;
        HostEpoch = hostEpoch;
    }

    /// <summary>The hulls left for the holder; empty while the first host still takes back its own attempt.</summary>
    public Guid[] ShipIds => shipIds ?? Array.Empty<Guid>();
}
