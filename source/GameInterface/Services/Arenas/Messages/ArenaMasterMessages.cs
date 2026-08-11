using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Arenas.Messages;

public readonly struct AddMetArenaMasterAndKnowTournaments : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public AddMetArenaMasterAndKnowTournaments(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}

public readonly struct AddMetArenaMaster : IEvent
{
    public readonly Hero MainHero;
    public readonly Settlement CurrentSettlement;

    public AddMetArenaMaster(
        Hero mainHero,
        Settlement currentSettlement)
    {
        MainHero = mainHero;
        CurrentSettlement = currentSettlement;
    }
}
