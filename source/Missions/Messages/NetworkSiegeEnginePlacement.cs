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

    public NetworkSiegeEnginePlacement(int pointId, string weaponTypeName)
    {
        PointId = pointId;
        WeaponTypeName = weaponTypeName;
    }
}
