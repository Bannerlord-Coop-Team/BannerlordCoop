using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Messages.LordConversations;

public readonly struct LordDefeatToRelease : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero ConversationHero;

    public LordDefeatToRelease(
        Hero mainHero,
        Hero conversationHero)
    {
        MainHero = mainHero;
        ConversationHero = conversationHero;
    }
}
