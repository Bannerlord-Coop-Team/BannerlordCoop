using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Messages;

public readonly struct PlayerLeftSoldiersBehind : IEvent
{
    public readonly Hero MainHero;
    public readonly MobileParty MainParty;

    public PlayerLeftSoldiersBehind(Hero mainHero, MobileParty mainParty)
    {
        MainHero = mainHero;
        MainParty = mainParty;
    }
}
