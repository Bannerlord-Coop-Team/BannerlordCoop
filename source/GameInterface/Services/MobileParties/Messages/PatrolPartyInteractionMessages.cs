using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.MobileParties.Messages;

public readonly struct AddPatrolPartyInteraction : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement PartyHomeSettlement;
    public readonly CampaignTime CampaignTime;

    public AddPatrolPartyInteraction(
        Hero mainHero,
        Settlement partyHomeSettlement,
        CampaignTime campaignTime)
    {
        MainHero = mainHero;
        PartyHomeSettlement = partyHomeSettlement;
        CampaignTime = campaignTime;
    }
}

public readonly struct PatrolPartyHostileAction : IEvent
{
    public readonly PartyBase MainParty;
    public readonly PartyBase ConversationParty;

    public PatrolPartyHostileAction(
        PartyBase mainParty,
        PartyBase conversationParty)
    {
        MainParty = mainParty;
        ConversationParty = conversationParty;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkAddPatrolPartyInteraction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainHeroId;

    [ProtoMember(2)]
    public readonly string PartyHomeSettlementId;

    [ProtoMember(3)]
    public readonly long CampaignTimeNumTicks;

    public NetworkAddPatrolPartyInteraction(
        string mainHeroId,
        string partyHomeSettlementId,
        long campaignTimeNumTicks)
    {
        MainHeroId = mainHeroId;
        PartyHomeSettlementId = partyHomeSettlementId;
        CampaignTimeNumTicks = campaignTimeNumTicks;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPatrolPartyHostileAction : ICommand
{
    [ProtoMember(1)]
    public readonly string MainPartyId;

    [ProtoMember(2)]
    public readonly string ConversationPartyId;

    public NetworkPatrolPartyHostileAction(
        string mainPartyId,
        string conversationPartyId)
    {
        MainPartyId = mainPartyId;
        ConversationPartyId = conversationPartyId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkPatrolPartyDestroyed : ICommand
{
    [ProtoMember(1)]
    public readonly string PatrolHomeSettlementId;

    public NetworkPatrolPartyDestroyed(string patrolHomeSettlementId)
    {
        PatrolHomeSettlementId = patrolHomeSettlementId;
    }
}