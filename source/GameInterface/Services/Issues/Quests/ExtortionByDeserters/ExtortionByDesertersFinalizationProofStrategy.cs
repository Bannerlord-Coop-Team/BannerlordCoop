using GameInterface.Services.Issues.Framework.Interface;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Handlers;
using GameInterface.Services.Issues.Quests.ExtortionByDeserters.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
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
            var ownerParty = Hero.MainHero.PartyBelongedTo;
            if (ownerParty == null || raid?.IsRaid != true || raid.HasWinner || raid.IsFinalized ||
                raid.AttackerSide.LeaderParty != party.Party ||
                raid.DefenderSide.LeaderParty != deserterQuest.QuestSettlement.Party ||
                ownerParty.CurrentSettlement != deserterQuest.QuestSettlement ||
                (ownerParty.MapEvent != null && ownerParty.MapEvent != raid) ||
                deserterQuest._defenderMobileParty != null) return false;

            deserterQuest.CreateDefenderParty();
            deserterQuest._defenderMobileParty.MapEventSide = raid.DefenderSide;
            ownerParty.MapEventSide = raid.DefenderSide;
            party.IgnoreByOtherPartiesTill(CampaignTime.Now - CampaignTime.Hours(1f));
            if (deserterQuest.QuestSettlement.MilitiaPartyComponent != null)
                deserterQuest.QuestSettlement.MilitiaPartyComponent.MobileParty.MapEventSide = null;
            if (ContainerProvider.TryResolve<ExtortionByDesertersHandler>(out var sync)) sync.Send(deserterQuest, ambush: true);
            return true;
        }

        if ((ExtortionByDesertersBranch)proof == ExtortionByDesertersBranch.BattleCleanup)
        {
            if (!deserterQuest._deserterBattleFinalizedForTheFirstTime) return false;
            deserterQuest._deserterBattleFinalizedForTheFirstTime = false;
            if (deserterQuest._currentState == State.DesertersAreDefeated)
            {
                deserterQuest.DestroyDeserterParty();
                deserterQuest._deserterMobileParty = null;
            }
            deserterQuest.DestroyDefenderParty();
            deserterQuest._defenderMobileParty = null;
            if (ContainerProvider.TryResolve<ExtortionByDesertersHandler>(out var sync)) sync.Send(deserterQuest);
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

        deserterQuest.AddLog(deserterQuest.OnQuestSucceededLogText);
        deserterQuest.CompleteQuestWithSuccess();
        return true;
    }
}
