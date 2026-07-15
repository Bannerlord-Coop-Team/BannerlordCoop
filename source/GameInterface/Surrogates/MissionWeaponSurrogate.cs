using ProtoBuf;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace GameInterface.Surrogates;

// Not used right now. Will probably be useful for syncing clients changing weapons mid-battle
// i.e. Dropping swords, picking up arrows etc.
[ProtoContract]
internal struct MissionWeaponSurrogate
{
    [ProtoMember(1)]
    public string ItemObjectId { get; set; }

    [ProtoMember(2)]
    public ItemModifier ItemModifier { get; set; }

    [ProtoMember(3)]
    public Banner Banner { get; set; }

    [ProtoMember(4)]
    public short DataValue { get; set; }

    [ProtoMember(5)]
    public short ReloadPhase { get; set; }

    [ProtoMember(6)]
    public MissionWeapon.MissionSubWeapon AmmoWeapon { get; set; }

    public MissionWeaponSurrogate(MissionWeapon missionWeapon)
    {
        ItemObjectId = missionWeapon.Item?.StringId;
        ItemModifier = missionWeapon.ItemModifier;
        Banner = missionWeapon.Banner;
        DataValue = missionWeapon._dataValue;
        ReloadPhase = missionWeapon.ReloadPhase;
        AmmoWeapon = missionWeapon._ammoWeapon;
    }

    public static implicit operator MissionWeaponSurrogate(MissionWeapon missionWeapon)
    {
        return new MissionWeaponSurrogate(missionWeapon);
    }

    public static implicit operator MissionWeapon(MissionWeaponSurrogate surrogate)
    {
        var item = string.IsNullOrEmpty(surrogate.ItemObjectId)
            ? null
            : MBObjectManager.Instance.GetObject<ItemObject>(surrogate.ItemObjectId);

        return new MissionWeapon(
            item,
            surrogate.ItemModifier,
            surrogate.Banner,
            surrogate.DataValue,
            surrogate.ReloadPhase,
            surrogate.AmmoWeapon.Value);
    }
}
