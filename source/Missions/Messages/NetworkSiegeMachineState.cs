using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Mission host → peers (over the mesh): one siege machine's gameplay state. Sent whenever a field
/// changes and replayed to joiners; appliers are idempotent. Machines are scene-placed, so their
/// MissionObjectId matches across clients loading the same scene and levels.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeMachineState : IEvent
{
    [ProtoMember(1)]
    public readonly int MachineId;
    /// <summary>Destructible hit points; -1 when the machine has no destruction component.</summary>
    [ProtoMember(2)]
    public readonly float HitPoints;
    /// <summary>Destruction state index; -1 when not destructible.</summary>
    [ProtoMember(3)]
    public readonly int DestructionState;
    /// <summary>CastleGate.GateState; -1 for non-gates.</summary>
    [ProtoMember(4)]
    public readonly int GateState;
    /// <summary>SiegeLadder.LadderState; -1 for non-ladders.</summary>
    [ProtoMember(5)]
    public readonly int LadderState;
    /// <summary>Path progress of a ram/tower; -1 when the machine has no movement component.</summary>
    [ProtoMember(6)]
    public readonly float MoveDistance;
    [ProtoMember(7)]
    public readonly bool HasArrived;

    public NetworkSiegeMachineState(int machineId, float hitPoints, int destructionState, int gateState, int ladderState, float moveDistance, bool hasArrived)
    {
        MachineId = machineId;
        HitPoints = hitPoints;
        DestructionState = destructionState;
        GateState = gateState;
        LadderState = ladderState;
        MoveDistance = moveDistance;
        HasArrived = hasArrived;
    }
}
