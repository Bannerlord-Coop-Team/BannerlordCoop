using ProtoBuf;

namespace Missions.Data;

[ProtoContract(SkipConstructor = true)]
public class MissionSubWeaponData
{
    [ProtoMember(1)]
    public readonly MissionWeaponData Value;

    public MissionSubWeaponData(MissionWeaponData value)
    {
        Value = value;
    }
}
