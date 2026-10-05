using Common.Messaging;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// Owner to peers over the mission mesh as the owner leaves: its hull's last rope states. Hull samples stop with
/// the owner, so without this a peer would keep the rope where the last sample left it.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleRopeFinal : IEvent
{
    [ProtoMember(1)] public readonly Guid ShipId;
    [ProtoMember(2)] public readonly string OwnerControllerId;
    [ProtoMember(3)] public readonly BattleRopeState[] Ropes;

    public NetworkBattleRopeFinal(Guid shipId, string ownerControllerId, BattleRopeState[] ropes)
    {
        ShipId = shipId;
        OwnerControllerId = ownerControllerId;
        Ropes = ropes;
    }
}
