using GameInterface.Services.Issues.Framework.Interface;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

internal enum ExtortionByDesertersBranch : byte
{
    FullReward = 1,
    HalfReward,
    RefuseReward,
    BattleCleanup,
    Ambush,
}

/// <summary>Runs each reward choice once, as the player whose deserters were defeated.</summary>
internal class ExtortionByDesertersFinalizationProofStrategy : IFinalizationProofStrategy
{
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest deserterQuest) || !quest.IsOngoing)
        {
            return false;
        }

        if ((ExtortionByDesertersBranch)proof == ExtortionByDesertersBranch.Ambush)
        {
            var party = deserterQuest._deserterMobileParty;
            var raid = party?.MapEvent;
            if (raid?.IsRaid != true || raid.AttackerSide.LeaderParty != party.Party ||
                raid.DefenderSide.LeaderParty != deserterQuest.QuestSettlement.Party ||
                TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.CurrentSettlement != deserterQuest.QuestSettlement ||
                deserterQuest._defenderMobileParty != null) return false;

            deserterQuest.CreateDefenderParty();
            deserterQuest._defenderMobileParty.MapEventSide = raid.DefenderSide;
            TaleWorlds.CampaignSystem.Party.MobileParty.MainParty.MapEventSide = raid.DefenderSide;
            party.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
            if (deserterQuest.QuestSettlement.MilitiaPartyComponent != null)
                deserterQuest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
            if (ContainerProvider.TryResolve<ExtortionByDesertersSync>(out var sync)) sync.Send(deserterQuest, ambush: true);
            return true;
        }

        if ((ExtortionByDesertersBranch)proof == ExtortionByDesertersBranch.BattleCleanup)
        {
            if (!deserterQuest._deserterBattleFinalizedForTheFirstTime) return false;
            deserterQuest._deserterBattleFinalizedForTheFirstTime = false;
            if (deserterQuest._currentState == State.DesertersAreDefeated) deserterQuest.DestroyDeserterParty();
            deserterQuest.DestroyDefenderParty();
            return true;
        }

        if (deserterQuest._currentState != State.DesertersAreDefeated) return false;

        switch ((ExtortionByDesertersBranch)proof)
        {
            case ExtortionByDesertersBranch.FullReward:
                deserterQuest.ApplyQuestResult(deserterQuest._questResultSuccess1);
                break;
            case ExtortionByDesertersBranch.HalfReward:
                deserterQuest.ApplyQuestResult(deserterQuest._questResultSuccess2);
                break;
            case ExtortionByDesertersBranch.RefuseReward:
                deserterQuest.ApplyQuestResult(deserterQuest._questResultSuccess3);
                break;
            default:
                return false;
        }

        deserterQuest.CompleteQuestWithSuccess();
        return true;
    }
}
