using Common.Messaging;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BattleWreckages.Messages;

public readonly struct WreckageEncounterLeft : IEvent
{
    public readonly Hero MainHero;
    public readonly BattleWreckageCampaignBehavior.WreckageLeaveReason Reason;

    public WreckageEncounterLeft(
        Hero mainHero,
        BattleWreckageCampaignBehavior.WreckageLeaveReason reason)
    {
        MainHero = mainHero;
        Reason = reason;
    }
}
