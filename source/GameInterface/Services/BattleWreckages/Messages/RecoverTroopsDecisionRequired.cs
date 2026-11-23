using Common.Messaging;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct RecoverTroopsDecisionRequired : IEvent
{
    public readonly Hero MainHero;
    public readonly int RecoverTroopCount;
    public readonly CampaignTime RequiredInvestigationDuration;

    public RecoverTroopsDecisionRequired(
        Hero mainHero,
        int recoverTroopCount,
        CampaignTime requiredInvestigationDuration)
    {
        MainHero = mainHero;
        RecoverTroopCount = recoverTroopCount;
        RequiredInvestigationDuration = requiredInvestigationDuration;
    }
}
