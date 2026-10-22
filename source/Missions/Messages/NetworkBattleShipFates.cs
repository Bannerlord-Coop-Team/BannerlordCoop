using Common.Messaging;
using GameInterface.Services.Ships.Data;
using ProtoBuf;
using System;

namespace Missions.Messages;

/// <summary>
/// [Client to server] The fates of the naval hulls a client simulates in a battle, numbered by <see cref="Revision"/> at the
/// sender's <see cref="HostEpoch"/>, so the server keeps the newest per ship until it applies them.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public sealed class NetworkBattleShipFates : ICommand
{
    [ProtoMember(1)] public readonly string InstanceId;
    [ProtoMember(2)] public readonly int HostEpoch;
    [ProtoMember(3)] public readonly long Revision;
    [ProtoMember(4)] public readonly BattleShipFate[] Fates;

    public NetworkBattleShipFates(string instanceId, int hostEpoch, long revision, BattleShipFate[] fates)
    {
        InstanceId = instanceId;
        HostEpoch = hostEpoch;
        Revision = revision;
        Fates = fates ?? Array.Empty<BattleShipFate>();
    }
}
