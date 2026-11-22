using Common.Logging;
using GameInterface.Services.Issues.Framework.Interface;
using Serilog;
using TaleWorlds.CampaignSystem;
using Quest = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest;
using State = TaleWorlds.CampaignSystem.Issues.ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssueQuest.ExtortionByDesertersQuestState;

namespace GameInterface.Services.Issues.Quests.ExtortionByDeserters;

/// <summary>Runs each reward choice once, as the player whose deserters were defeated.</summary>
internal class ExtortionByDesertersFinalizationProofStrategy : IFinalizationProofStrategy
{
    private static readonly ILogger Logger = LogManager.GetLogger<ExtortionByDesertersFinalizationProofStrategy>();
    public bool TryRunBranch(QuestBase quest, byte proof)
    {
        if (!(quest is Quest deserterQuest)) return false;

        if (!quest.IsOngoing)
        {
            Logger.Warning("Skipping extortion branch {proof} for non-ongoing quest {quest}", proof, quest.StringId);
            return true;
        }

        if (deserterQuest._currentState != State.DesertersAreDefeated)
        {
            Logger.Warning("Skipping extortion branch {proof} for quest {quest} in state {state}",
                proof, deserterQuest.StringId, deserterQuest._currentState);
            return true;
        }

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
