using ProtoBuf;

namespace GameInterface.Services.MobileParties.Data;

/// <summary>
/// Data for changing the army of a party.
/// </summary>
[ProtoContract(SkipConstructor = true)]
public class PartyArmyChangeData
{
    public PartyArmyChangeData(string partyId, string armyId)
    {
        PartyId = partyId;
        ArmyId = armyId;
    }

    [ProtoMember(1)]
    public string PartyId { get; }
    [ProtoMember(2)]
    public string ArmyId { get; }
}
