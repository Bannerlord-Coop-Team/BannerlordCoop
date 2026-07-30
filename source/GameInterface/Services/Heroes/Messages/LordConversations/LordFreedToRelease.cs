using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

public readonly struct LordFreedToRelease : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero ConversationHero;

    public LordFreedToRelease(
        Hero mainHero,
        Hero conversationHero)
    {
        MainHero = mainHero;
        ConversationHero = conversationHero;
    }
}
