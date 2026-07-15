using ProtoBuf;
using TaleWorlds.MountAndBlade;
using static TaleWorlds.MountAndBlade.MissionWeapon;

namespace GameInterface.Surrogates;

// Not used right now. Will probably be useful for syncing clients changing weapons mid-battle
// i.e. Dropping swords, picking up arrows etc.
[ProtoContract]
internal struct MissionSubWeaponSurrogate
{
    [ProtoMember(1)]
    public MissionWeapon Value { get; set; }

    public MissionSubWeaponSurrogate(MissionSubWeapon missionSubWeapon)
    {
        Value = missionSubWeapon.Value;
    }

    public static implicit operator MissionSubWeaponSurrogate(MissionSubWeapon missionSubWeapon)
    {
        return new MissionSubWeaponSurrogate(missionSubWeapon);
    }

    public static implicit operator MissionSubWeapon(MissionSubWeaponSurrogate surrogate)
    {
        return new MissionSubWeapon(surrogate.Value);
    }
}
