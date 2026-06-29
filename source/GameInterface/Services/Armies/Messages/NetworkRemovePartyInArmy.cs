using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Armies.Messages;

/// <summary>
/// Command to remove a MobileParty from an Army
/// </summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRemovePartyInArmy : ICommand
{
    [ProtoMember(1)]
    public readonly string ArmyId;
    [ProtoMember(2)]
    public readonly string MobilePartyId;
    [ProtoMember(3)]
    public readonly string ClientMobilePartyId;

    public NetworkRemovePartyInArmy(string armyId, string mobilePartyId, string clientMobilePartyId)
    {
        ArmyId = armyId;
        MobilePartyId = mobilePartyId;
        ClientMobilePartyId = clientMobilePartyId;
    }
}
