using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Siege machine simulator → peers (over the mesh): one machine's gameplay state. Sent whenever a
/// field changes and replayed to joiners; appliers are idempotent. Machines are scene-placed, so
/// their MissionObjectId matches across clients loading the same scene and levels.
/// </summary>
// The int sentinels are shifted by one on the wire so protobuf omits their usual -1 instead of a 10-byte varint.
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeMachineState : IEvent
{
    [ProtoMember(1)]
    public readonly int MachineId;
    /// <summary>Destructible hit points; -1 when the machine has no destruction component.</summary>
    [ProtoMember(2)]
    public readonly float HitPoints;
    /// <summary>Destruction state index; -1 when not destructible.</summary>
    public int DestructionState => destructionStateWire - 1;
    [ProtoMember(3)]
    private readonly int destructionStateWire;
    /// <summary>CastleGate.GateState; -1 for non-gates.</summary>
    public int GateState => gateStateWire - 1;
    [ProtoMember(4)]
    private readonly int gateStateWire;
    /// <summary>SiegeLadder.LadderState; -1 for non-ladders.</summary>
    public int LadderState => ladderStateWire - 1;
    [ProtoMember(5)]
    private readonly int ladderStateWire;
    /// <summary>Path progress of a ram/tower; -1 when the machine has no movement component.</summary>
    [ProtoMember(6)]
    public readonly float MoveDistance;
    [ProtoMember(7)]
    public readonly bool HasArrived;
    /// <summary>RangedSiegeWeapon.WeaponState; -1 for non-ranged machines. Drives the peer's wind-up/reload arm animation.</summary>
    public int WeaponState => weaponStateWire - 1;
    [ProtoMember(8)]
    private readonly int weaponStateWire;
    /// <summary>The simulator's aim targets (RangedSiegeWeapon.TargetDirection/TargetReleaseAngle);
    /// -1000 sentinel for non-ranged or non-simulated machines (-1 collides with a valid angle).
    /// Receivers replay vanilla's own speed-limited approach toward them, so the body turns smoothly.</summary>
    [ProtoMember(9)]
    public readonly float AimDirection;
    [ProtoMember(10)]
    public readonly float AimReleaseAngle;
    /// <summary>
    /// BR-102: the sender's host epoch for this battle. Receivers drop a snapshot stamped by an
    /// earlier hosting generation (a deposed host's host-owned fields in flight across a migration);
    /// 0 = unstamped (sender had no assignment yet), always accepted.
    /// </summary>
    [ProtoMember(11)]
    public readonly int HostEpoch;
    /// <summary>StonePile.AmmoCount; -1 for non-stone-pile machines.</summary>
    public int StoneAmmo => stoneAmmoWire - 1;
    [ProtoMember(12)]
    private readonly int stoneAmmoWire;
    /// <summary>Whether <see cref="StoneAmmo"/> was captured by the sender.</summary>
    [ProtoMember(13)]
    public readonly bool HasStoneAmmo;
    /// <summary>Controller that captured this snapshot.</summary>
    [ProtoMember(14)]
    public readonly string SenderControllerId;
    /// <summary>Per-machine claim generation known by the sender.</summary>
    [ProtoMember(15)]
    public readonly int AuthorityRevision;
    public int MangonelAmmo => mangonelAmmoWire - 1;
    [ProtoMember(16)]
    private readonly int mangonelAmmoWire;
    [ProtoMember(17)]
    public readonly bool HasMangonelAmmo;
    public NetworkSiegeMachineState(
        int machineId,
        float hitPoints,
        int destructionState,
        int gateState,
        int ladderState,
        float moveDistance,
        bool hasArrived,
        int weaponState,
        float aimDirection,
        float aimReleaseAngle,
        int hostEpoch = 0,
        int stoneAmmo = -1,
        string senderControllerId = null,
        int authorityRevision = 0,
        int mangonelAmmo = -1)
    {
        MachineId = machineId;
        HitPoints = hitPoints;
        destructionStateWire = destructionState + 1;
        gateStateWire = gateState + 1;
        ladderStateWire = ladderState + 1;
        MoveDistance = moveDistance;
        HasArrived = hasArrived;
        weaponStateWire = weaponState + 1;
        AimDirection = aimDirection;
        AimReleaseAngle = aimReleaseAngle;
        HostEpoch = hostEpoch;
        stoneAmmoWire = stoneAmmo + 1;
        HasStoneAmmo = stoneAmmo >= 0;
        SenderControllerId = senderControllerId;
        AuthorityRevision = authorityRevision;
        mangonelAmmoWire = mangonelAmmo + 1;
        HasMangonelAmmo = mangonelAmmo >= 0;
    }
}
