using ProtoBuf;
using System.Collections.Generic;

namespace Missions.Data;

[ProtoContract(SkipConstructor = true)]
public class MissionEquipmentData
{
    [ProtoMember(1)]
    public List<MissionWeaponData> WeaponSlots;

    public MissionEquipmentData(List<MissionWeaponData> weaponSlots)
    {
        WeaponSlots = weaponSlots;
    }
}
