using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Clans.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct UpdatePartyBehaviorOnSelection : ICommand
{
    [ProtoMember(1)]
    public readonly string MobilePartyId;

    [ProtoMember(2)]
    public readonly MobileParty.PartyObjective PartyObjective;

    public UpdatePartyBehaviorOnSelection(
        string mobilePartyId,
        MobileParty.PartyObjective partyObjective)
    {
        MobilePartyId = mobilePartyId;
        PartyObjective = partyObjective;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeAutoRecruitForSettlement : ICommand
{
    [ProtoMember(1)]
    public readonly string HomeSettlementId;

    [ProtoMember(2)]
    public readonly bool Value;

    public ChangeAutoRecruitForSettlement(
        string homeSettlementId,
        bool value)
    {
        HomeSettlementId = homeSettlementId;
        Value = value;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeAutoRecruitForSettlementClients : ICommand
{
    [ProtoMember(1)]
    public readonly string HomeSettlementId;

    [ProtoMember(2)]
    public readonly bool Value;

    public ChangeAutoRecruitForSettlementClients(
        string homeSettlementId,
        bool value)
    {
        HomeSettlementId = homeSettlementId;
        Value = value;
    }
}