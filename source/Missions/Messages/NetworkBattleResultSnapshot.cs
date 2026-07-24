using Common.Messaging;
using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Messages;

/// <summary>Host result catch-up for a client that entered the mission after the battle resolved.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkBattleResultSnapshot : IEvent
{
    [ProtoMember(1)]
    public string InstanceId { get; }

    [ProtoMember(2)]
    public string HostControllerId { get; }

    [ProtoMember(3)]
    public int HostEpoch { get; }

    [ProtoMember(4)]
    public BattleState BattleState { get; }

    public NetworkBattleResultSnapshot(
        string instanceId,
        string hostControllerId,
        int hostEpoch,
        BattleState battleState)
    {
        InstanceId = instanceId;
        HostControllerId = hostControllerId;
        HostEpoch = hostEpoch;
        BattleState = battleState;
    }
}
