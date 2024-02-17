using ProtoBuf;

namespace GameInterface.Services.Armies.Data;

/// <summary>
/// Data for adding a party to an army.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public record ArmyAddPartyData
{
    public ArmyAddPartyData(string armyStringId, string partyStringId)
    {
        ArmyStringId = armyStringId;
        PartyStringId = partyStringId;
    }

    [ProtoMember(1)]
    public string ArmyStringId { get; set; }
    [ProtoMember(2)]
    public string PartyStringId { get; set; }

}
