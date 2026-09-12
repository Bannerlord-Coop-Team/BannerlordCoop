using Common.Messaging;
using ProtoBuf;

namespace Missions.Messages;

/// <summary>
/// Engine deployer → peers (over the mesh): a deployment point deployed a siege engine (or disbanded
/// it, when the type name is null/empty). Points are scene-placed, so their MissionObjectId matches
/// across clients loading the same scene and levels.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class NetworkSiegeEnginePlacement : IEvent
{
    [ProtoMember(1)]
    public readonly int PointId;
    [ProtoMember(2)]
    public readonly string WeaponTypeName;
    /// <summary>
    /// BR-102: the deploying host's epoch for this battle. Placement mutates vanilla's ordered
    /// undeployed-weapon lists (order-sensitive, replayed to joiners), so receivers drop a placement
    /// stamped by an earlier hosting generation (a deposed deployer in flight across a migration);
    /// 0 = unstamped (sender had no assignment yet), always accepted.
    /// </summary>
    [ProtoMember(3)]
    public readonly int HostEpoch;

    public NetworkSiegeEnginePlacement(int pointId, string weaponTypeName, int hostEpoch = 0)
    {
        PointId = pointId;
        WeaponTypeName = weaponTypeName;
        HostEpoch = hostEpoch;
    }
}
