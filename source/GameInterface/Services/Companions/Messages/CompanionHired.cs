using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.Companions.Messages;

public readonly struct CompanionHired : IEvent
{
    public readonly Hero MainHero;
    public readonly Hero OneToOneConversationHero;
    public readonly int HiringPrice;
    public readonly Clan PlayerClan;
    public readonly MobileParty MainParty;

    public CompanionHired(
        Hero mainHero,
        Hero oneToOneConversationHero,
        int hiringPrice,
        Clan playerClan,
        MobileParty mainParty)
    {
        MainHero = mainHero;
        OneToOneConversationHero = oneToOneConversationHero;
        HiringPrice = hiringPrice;
        PlayerClan = playerClan;
        MainParty = mainParty;
    }
}