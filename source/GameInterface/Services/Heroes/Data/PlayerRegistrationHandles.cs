using ProtoBuf;

namespace GameInterface.Services.Heroes.Data;

[ProtoContract(SkipConstructor = true)]
public readonly struct PlayerRegistrationHandles
{
    [ProtoMember(1)] public readonly uint Hero;
    [ProtoMember(2)] public readonly uint MobileParty;
    [ProtoMember(3)] public readonly uint Clan;
    [ProtoMember(4)] public readonly uint CharacterObject;
    [ProtoMember(5)] public readonly uint HeroDeveloper;
    [ProtoMember(6)] public readonly uint ItemRoster;
    [ProtoMember(7)] public readonly uint PartyBase;
    [ProtoMember(8)] public readonly uint MemberRoster;
    [ProtoMember(9)] public readonly uint PrisonRoster;

    public PlayerRegistrationHandles(
        uint hero,
        uint mobileParty,
        uint clan,
        uint characterObject,
        uint heroDeveloper,
        uint itemRoster,
        uint partyBase,
        uint memberRoster,
        uint prisonRoster)
    {
        Hero = hero;
        MobileParty = mobileParty;
        Clan = clan;
        CharacterObject = characterObject;
        HeroDeveloper = heroDeveloper;
        ItemRoster = itemRoster;
        PartyBase = partyBase;
        MemberRoster = memberRoster;
        PrisonRoster = prisonRoster;
    }
}
