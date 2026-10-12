using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.NavalDLC;

/// <summary>
/// Troops a player sent away to deliver a ship, saved like ClanFleetManagementCampaignBehavior.SentTroopsData.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class SentTroopsData
{
    [ProtoMember(1)]
    public readonly List<SentTroopData> Troops;

    [ProtoMember(2)]
    public readonly long ReturnTimeTicks;

    [ProtoMember(3)]
    public readonly string ShipName;

    [ProtoMember(4)]
    public readonly string PartyName;

    public SentTroopsData(List<SentTroopData> troops, long returnTimeTicks, string shipName, string partyName)
    {
        Troops = troops;
        ReturnTimeTicks = returnTimeTicks;
        ShipName = shipName;
        PartyName = partyName;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct SentTroopData
{
    [ProtoMember(1)]
    public readonly string CharacterId;

    [ProtoMember(2)]
    public readonly int Number;

    [ProtoMember(3)]
    public readonly int WoundedNumber;

    [ProtoMember(4)]
    public readonly int Xp;

    public SentTroopData(string characterId, int number, int woundedNumber, int xp)
    {
        CharacterId = characterId;
        Number = number;
        WoundedNumber = woundedNumber;
        Xp = xp;
    }
}
