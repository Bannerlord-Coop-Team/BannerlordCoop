using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

public readonly struct LiberateLordPrisoner : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero ConversationHero;

    public LiberateLordPrisoner(
        Hero mainHero,
        Hero conversationHero)
    {
        MainHero = mainHero;
        ConversationHero = conversationHero;
    }
}
