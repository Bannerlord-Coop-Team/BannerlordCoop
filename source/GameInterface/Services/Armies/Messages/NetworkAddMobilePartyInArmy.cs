using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to add a MobileParty to an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAddMobilePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string MobilePartyId;

    public NetworkAddMobilePartyInArmy(string armyId, string mobilePartyId)
    {
        ArmyId = armyId;
        MobilePartyId = mobilePartyId;
    }
}
