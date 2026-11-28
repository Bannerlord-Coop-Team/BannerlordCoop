using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.BattleWreckages;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageEncounterStarted : IEvent
{
    public readonly BattleWreckage BattleWreckage;
    public readonly Hero MainHero;
    public readonly bool IsInterrupted;
    public readonly CampaignTime RequiredInvestigationDuration;

    public WreckageEncounterStarted(
        BattleWreckage battleWreckage,
        Hero mainHero,
        bool isInterrupted,
        CampaignTime requiredInvestigationDuration)
    {
        BattleWreckage = battleWreckage;
        MainHero = mainHero;
        IsInterrupted = isInterrupted;
        RequiredInvestigationDuration = requiredInvestigationDuration;
    }
}
