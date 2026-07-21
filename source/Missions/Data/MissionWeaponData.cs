using ProtoBuf;
using TaleWorlds.Core;

namespace Missions.Data;

[ProtoContract(SkipConstructor = true)]
public class MissionWeaponData
{
    [ProtoMember(1)]
    public readonly string ItemObjectId;

    [ProtoMember(2)]
    public readonly ItemModifier ItemModifier;

    [ProtoMember(3)]
    public readonly Banner Banner;

    [ProtoMember(4)]
    public readonly short DataValue;

    [ProtoMember(5)]
    public readonly short ReloadPhase;

    [ProtoMember(6)]
    public readonly MissionSubWeaponData AmmoWeaponData;

    public MissionWeaponData(
        string itemObjectId,
        ItemModifier itemModifier,
        Banner banner,
        short dataValue,
        short reloadPhase,
        MissionSubWeaponData ammoWeaponData)
    {
        ItemObjectId = itemObjectId;
        ItemModifier = itemModifier;
        Banner = banner;
        DataValue = dataValue;
        ReloadPhase = reloadPhase;
        AmmoWeaponData = ammoWeaponData;
    }
}
