using Common.Messaging;
using GameInterface.Services.Villages.Data;
using ProtoBuf;

namespace GameInterface.Services.Villages.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkVillageHostileActionStarted : ICommand
{
    [ProtoMember(1)]
    public readonly VillageHostileAction Action;
    [ProtoMember(2)]
    public readonly string MobilePartyId;
    [ProtoMember(3)]
    public readonly string SettlementId;
    /// <summary>The village gave in without a battle; the outcome is already applied.</summary>
    [ProtoMember(4)]
    public readonly bool Unresisted;

    public NetworkVillageHostileActionStarted(
        VillageHostileAction action,
        string mobilePartyId,
        string settlementId,
        bool unresisted = false)
    {
        Action = action;
        MobilePartyId = mobilePartyId;
        SettlementId = settlementId;
        Unresisted = unresisted;
    }
}
