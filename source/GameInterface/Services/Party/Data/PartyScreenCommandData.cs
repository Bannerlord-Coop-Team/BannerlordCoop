using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Party.Data;

/// <summary>
/// One party screen command as the player ran it. Number and WoundedNumber are what the command actually moved.
/// </summary>
[ProtoContract(SkipConstructor = true)]
internal readonly struct PartyScreenCommandData
{
    [ProtoMember(1)]
    public readonly PartyScreenLogic.PartyCommandCode Code;

    [ProtoMember(2)]
    public readonly PartyScreenLogic.PartyRosterSide Side;

    [ProtoMember(3)]
    public readonly PartyScreenLogic.TroopType Type;

    [ProtoMember(4)]
    public readonly uint CharacterId;

    [ProtoMember(5)]
    public readonly int Number;

    [ProtoMember(6)]
    public readonly int WoundedNumber;

    [ProtoMember(7)]
    public readonly int UpgradeTarget;

    public PartyScreenCommandData(
        PartyScreenLogic.PartyCommandCode code,
        PartyScreenLogic.PartyRosterSide side,
        PartyScreenLogic.TroopType type,
        uint characterId,
        int number,
        int woundedNumber,
        int upgradeTarget)
    {
        Code = code;
        Side = side;
        Type = type;
        CharacterId = characterId;
        Number = number;
        WoundedNumber = woundedNumber;
        UpgradeTarget = upgradeTarget;
    }
}
