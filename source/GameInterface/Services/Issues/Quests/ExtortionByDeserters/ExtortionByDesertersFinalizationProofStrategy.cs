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
