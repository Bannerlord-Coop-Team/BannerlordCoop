using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

public readonly struct TakeLordPrisoner : IEvent
{
    public readonly PartyBase MainParty;
    public readonly Hero ConversationHero;

    public TakeLordPrisoner(
        PartyBase mainParty,
        Hero conversationHero)
    {
        MainParty = mainParty;
        ConversationHero = conversationHero;
    }
}
